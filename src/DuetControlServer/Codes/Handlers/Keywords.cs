﻿using DuetAPI;
using DuetAPI.Commands;
using DuetAPI.ObjectModel;
using DuetControlServer.Files;
using DuetControlServer.Model;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Code = DuetControlServer.Commands.Code;

namespace DuetControlServer.Codes.Handlers
{
    /// <summary>
    /// Functions for interpreting meta G-code keywords
    /// </summary>
    public static class Keywords
    {
        /// <summary>
        /// Logger instance
        /// </summary>
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Process a non-branching meta G-code statement
        /// </summary>
        /// <param name="code">Code to process</param>
        /// <returns>Result of the code if the code completed</returns>
        /// <exception cref="OperationCanceledException">The code was cancelled</exception></exception>
        public static async Task<Message> Process(Code code)
        {
            if (code.KeywordArgument is null)
            {
                throw new ArgumentException("KeywordArgument must not be empty");
            }

            bool inSingleQuotes = false, startedSingleQuote = false, inDoubleQuotes = false;
            switch (code.Keyword)
            {
                case KeywordType.Echo:
                case KeywordType.Abort:
                    if (!await Processor.FlushAsync(code, false))
                    {
                        if (!code.CancellationToken.IsCancellationRequested)
                        {
                            // echo and abort may be executed only if the channel is active
                            return new Message();
                        }
                        throw new OperationCanceledException();
                    }

                    string? result;
                    if (code.Keyword == KeywordType.Echo)
                    {
                        string keywordArgument = code.KeywordArgument.TrimStart();
                        if (keywordArgument.StartsWith('>'))
                        {
                            // File redirection requested
                            bool append = keywordArgument.StartsWith(">>"), appendNoNL = keywordArgument.StartsWith(">>>");
                            keywordArgument = keywordArgument[(appendNoNL ? 3 : (append ? 2 : 1))..].TrimStart();

                            // Get the file string or expression to write to
                            bool isComplete = false;
                            int numCurlyBraces = 0;
                            string filenameExpression = string.Empty;
                            for (int i = 0; i < keywordArgument.Length; i++)
                            {
                                char c = keywordArgument[i];
                                if (inSingleQuotes)
                                {
                                    if (c == '\'' && !startedSingleQuote)
                                    {
                                        inSingleQuotes = false;
                                        isComplete = numCurlyBraces == 0;
                                    }
                                    startedSingleQuote = false;
                                }
                                else if (inDoubleQuotes)
                                {
                                    if (c == '"')
                                    {
                                        inDoubleQuotes = false;
                                        isComplete = numCurlyBraces == 0;
                                    }
                                }
                                else if (c == '\'')
                                {
                                    inSingleQuotes = startedSingleQuote = true;
                                }
                                else if (c == '"')
                                {
                                    inDoubleQuotes = true;
                                }
                                else if (c == '{')
                                {
                                    numCurlyBraces++;
                                }
                                else if (c == '}')
                                {
                                    numCurlyBraces--;
                                    isComplete = numCurlyBraces == 0;
                                }
                                else if (char.IsWhiteSpace(c))
                                {
                                    // Whitespaces after the initial > or >> are not permitted
                                    isComplete = numCurlyBraces == 0;
                                }

                                if (isComplete)
                                {
                                    if (i == 0)
                                    {
                                        return new Message(MessageType.Error, "Missing filename for file redirection");
                                    }

                                    filenameExpression = keywordArgument[..(i + 1)];
                                    code.KeywordArgument = keywordArgument[(i + 1)..];
                                    break;
                                }
                            }

                            // Evaluate the filename and result to write
                            string filename = await Expressions.EvaluateExpression(code, filenameExpression, false, false);
                            string physicalFilename = await FilePath.ToPhysicalAsync(filename, FileDirectory.System), parentDirectory = Path.GetDirectoryName(physicalFilename)!;
                            result = await Expressions.Evaluate(code, true);

                            // Write it to the designated file
                            _logger.Debug("{0} '{1}' to {2}", append ? "Appending" : "Writing", result, filename);

                            if (!Directory.Exists(parentDirectory))
                            {
                                Directory.CreateDirectory(parentDirectory);
                            }

                            await using (FileStream fs = new(physicalFilename, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, Settings.FileBufferSize))
                            {
                                await using StreamWriter writer = new(fs, Encoding.UTF8, Settings.FileBufferSize);
                                if (appendNoNL)
                                {
                                    await writer.WriteAsync(result);
                                }
                                else
                                {
                                    await writer.WriteLineAsync(result);
                                }
                            }

                            // Done
                            return new Message();
                        }
                    }
                    result = await Expressions.Evaluate(code, true);

                    if (code.Keyword == KeywordType.Abort)
                    {
                        await SPI.Interface.AbortAllAsync(code.Channel);
                    }
                    return new Message(MessageType.Success, result ?? string.Empty);

                case KeywordType.Global:
                case KeywordType.Var:
                case KeywordType.Set:
                    // Do not attempt to process cancelled codes
                    code.CancellationToken.ThrowIfCancellationRequested();

                    // Validate the keyword and expression first
                    string varName = string.Empty, expression = string.Empty;
                    bool inExpression = false, wantExpression = false;
                    int numSquareBrackets = 0;
                    foreach (char c in code.KeywordArgument)
                    {
                        if (inExpression)
                        {
                            expression += c;
                        }
                        else if (c == '=')
                        {
                            inExpression = true;
                        }
                        else if (numSquareBrackets > 0 || c == '[')
                        {
                            // Contents of square brackets are not trimmed
                            if (inSingleQuotes)
                            {
                                inSingleQuotes = c != '\'' || startedSingleQuote;
                                startedSingleQuote = false;
                            }
                            else if (inDoubleQuotes)
                            {
                                inDoubleQuotes = c != '"';
                            }
                            else if (c == '\'')
                            {
                                inSingleQuotes = startedSingleQuote = true;
                            }
                            else if (c == '"')
                            {
                                inDoubleQuotes = true;
                            }
                            else if (c == '[')
                            {
                                numSquareBrackets++;
                            }
                            else if (c == ']')
                            {
                                numSquareBrackets--;
                            }
                            varName += c;
                        }
                        else if (!char.IsWhiteSpace(c))
                        {
                            // Permit only a certain subset of chars for variable names
                            if (!char.IsLetterOrDigit(c) && c != '_' && (c != '.' || code.Keyword != KeywordType.Set) || wantExpression)
                            {
                                if (!await Processor.FlushAsync(code, false, ifExecuting: code.Keyword == KeywordType.Global))
                                {
                                    throw new OperationCanceledException();
                                }
                                throw new CodeParserException("expected '='", code);
                            }
                            varName += c;
                        }
                        else if (!string.IsNullOrEmpty(varName))
                        {
                            // Don't allow illegal variable names like "global. fo o", although a name like "global.foo [4]" is valid
                            wantExpression = true;
                        }
                    }

                    if (!await Processor.FlushAsync(code, false, ifExecuting: code.Keyword == KeywordType.Global || (code.Keyword == KeywordType.Set && varName.StartsWith("global."))))
                    {
                        // global and set global.* may be only executed if the corresponding channel is active
                        throw new OperationCanceledException();
                    }

                    // Check the variable and expression
                    if (string.IsNullOrWhiteSpace(varName))
                    {
                        throw new CodeParserException("expected a new variable name", code);
                    }
                    if (!inExpression)
                    {
                        throw new CodeParserException("expected '='", code);
                    }

                    // Replace SBC fields and prepare the variable name
                    expression = await Expressions.Evaluate(code, false) ?? string.Empty;
                    string fullVarName = varName;
                    if (code.Keyword == KeywordType.Set)
                    {
                        fullVarName = await Expressions.EvaluateExpression(code, fullVarName, true, false);
                    }
                    else
                    {
                        fullVarName = (code.Keyword == KeywordType.Global ? "global." : "var.") + varName;
                    }

                    // Assign the variable
                    object? varContent = await SPI.Interface.SetVariable(code.Channel, code.Keyword != KeywordType.Set, fullVarName, expression);
                    _logger.Debug("Set variable {0} to {1}", fullVarName, varContent);

                    // Keep track of it
                    if (code.Keyword == KeywordType.Var && code.File is not null)
                    {
                        using (await code.File.LockAsync())
                        {
                            code.File.AddLocalVariable(varName);
                        }
                    }
                    return new Message();
            }

            throw new NotSupportedException($"Unsupported keyword '{code.Keyword}'");
        }
    }
}

﻿using DuetAPI.Commands;
using DuetControlServer.Files;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.IO;
using System.Threading.Tasks;

namespace UnitTests.File
{
    [TestFixture]
    public class Position
    {
        [Test]
        public async Task TestPosition()
        {
            // NOTE: This test fails if the project isn't checked out as-is (i.e. if NL is converted to CRNL) .
            // In that case the positions and lengths below don't match
            string filePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "../../../File/GCodes/Cura.gcode");
            CodeFile file = new(System.IO.Path.GetFileName(filePath), filePath, DuetAPI.CodeChannel.File);
            Code code;

            // Line 1
            code = (await file.ReadCodeAsync())!;
            ClassicAssert.AreEqual(0, code.FilePosition);
            ClassicAssert.AreEqual(15, code.Length);

            // Line 2
            code = (await file.ReadCodeAsync())!;
            ClassicAssert.AreEqual(15, code.FilePosition);
            ClassicAssert.AreEqual(11, code.Length);

            // Line 3
            code = (await file.ReadCodeAsync())!;
            ClassicAssert.AreEqual(26, code.FilePosition);
            ClassicAssert.AreEqual(26, code.Length);

            // Go back to the first char of line 2. May be 16 if CRLF instead of NL is used
            file.Position = 15;

            // Read it again
            code = (await file.ReadCodeAsync())!;
            ClassicAssert.AreEqual(15, code.FilePosition);
            ClassicAssert.AreEqual(11, code.Length);
        }
    }
}

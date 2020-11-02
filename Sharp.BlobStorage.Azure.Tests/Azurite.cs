/*
    Copyright 2020 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
*/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Sharp.BlobStorage.Azure
{
    internal static class Azurite
    {
        public static bool IsRunning
            => Run("docker", "inspect blob", expectedExitCode: null)
                is (0, var output)
                && output.IndexOf("running", StringComparison.Ordinal) >= 0;

        public static void Start()
            => Run("docker", "run -d --rm --name blob -p 10000:10000 "
                + "mcr.microsoft.com/azure-storage/azurite "
                + "azurite-blob --blobHost 0.0.0.0");

        public static void Stop()
            => Run("docker", "kill blob");

        private static (int, string) Run(
            string program,
            string arguments,
            int?   expectedExitCode = 0)
        {
            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName               = program,
                Arguments              = arguments,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            var buffer = new StringBuilder();
            process.OutputDataReceived += (_, e) => buffer.Append(e.Data);
            process.ErrorDataReceived  += (_, e) => buffer.Append(e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            var exitCode = process.ExitCode;
            var output   = buffer.ToString();

            if (expectedExitCode is int c && c != exitCode)
                throw new ExternalException(
                    $"{program} exited with code {exitCode}.\n{output}"
                );

            return (exitCode, output);
        }
    }
}

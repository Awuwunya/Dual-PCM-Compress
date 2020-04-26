using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Dual_PCM_Compress {
	class Program {
		static void Main(string[] args) {
			if (args.Length < 4) {
				Console.Write(
					"Usage: \"Dual PCM Compress\" <driver bin> <settings> <output file> <compressor>\n" +
					"<driver bin> :  Binary output data for the Dual PCM driver\n" +
					"<settings> :    Settings file containing the data needed to fix the driver binary\n" +
					"<output file> : File to put the compressed data at\n" +
					"<compressor> :  The file location of koscmp.exe\n\n" +
					"This tool was mainly created to help with compressing Dual PCM ion a way that\n" +
					"doesn't suddenly break for no reason. This provides a more robust method for\n" +
					"correcting the driver binary and compressing the driver to Kosinski. Other\n" +
					"compressors could be used, so long as they expect the first argument to be the\n" +
					"input file and second the output file."
				);

				Console.ReadKey();
				return;
			}

			try {
				// figure out if arguments are ok
				if (!File.Exists(args[0])) {
					Console.WriteLine("Unable to read input file " + args[0]);
					Console.ReadKey();
					return;
				}

				if (!File.Exists(args[1])) {
					Console.WriteLine("Unable to read input file " + args[1]);
					Console.ReadKey();
					return;
				}

				if (!File.Exists(args[2])) {
					Console.WriteLine("Unable to read input file " + args[2]);
					Console.ReadKey();
					return;
				}

				// load settings and determine the addresses
				byte[] settings = File.ReadAllBytes(args[1]);
				int outaddr = ReadLong(settings, 0);
				int maxlen = ReadLong(settings, 4);

				long inlen = new FileInfo(args[2]).Length;

				if (inlen < outaddr) {
					Console.WriteLine("The destination address for Dual PCM is larger than the ROM file!");
					Console.ReadKey();
					return;
				}
				
				{ // fix the file
					byte[] data = File.ReadAllBytes(args[0]);

					for (int i = 8;i < settings.Length;) {
						data[(settings[i++] << 8) | settings[i++]] = settings[i++];

						if (settings[i++] != '>') {
							Console.WriteLine($"Unexpected delimiter {settings[i - 1]}");
							Console.ReadKey();
							return;
						}
					}

					File.WriteAllBytes(args[0], data);
				}

				{ // compress the file
					Process comp = Process.Start(new ProcessStartInfo(args[3], $"\"{args[0]}\" \"{args[0]}.kos\"") {
						WorkingDirectory = Directory.GetCurrentDirectory(),
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardError = true,
						RedirectStandardOutput = true,
					});

					comp.WaitForExit(3000);
					if (!comp.HasExited) comp.Kill();

					// check we succeeded
					if (!File.Exists(args[0] + ".kos")) {
						Console.WriteLine("Could not compress file correctly, unable to continue.");
						Console.ReadKey();
						return;
					}
				}

				long actuallen = new FileInfo(args[0] + ".kos").Length;

				if (actuallen <= 0) {
					Console.WriteLine("Could not compress file correctly, unable to continue.");
					Console.ReadKey();
					return;
				}

				// check if it fits
				if(actuallen > maxlen) {
					Console.WriteLine($"Compressed sound driver does not fit! Increase Z80_Space to ${actuallen.ToString("X")} and build again.");
					Console.ReadKey();
					return;
				}

				// copy the compressed file data
				using(FileStream fs = File.OpenWrite(args[2])) {
					fs.Seek(outaddr, SeekOrigin.Begin);

					using (FileStream cf = File.OpenRead(args[0] + ".kos")) {
						cf.CopyTo(fs, (int)actuallen);
					}

					fs.Flush();
				}

				// delete files and send message
				File.Delete(args[0] + ".kos");
				File.Delete(args[0]);
				File.Delete(args[1]);
				Console.WriteLine($"Success! Compressed driver size is ${actuallen.ToString("X")}!");

			} catch (Exception e) {
				Console.WriteLine(e);
				Console.ReadKey();
			}
		}

		private static int ReadLong(byte[] arr, int off) {
			return ((arr[off++] << 24) | (arr[off++] << 16) | (arr[off++] << 8) | arr[off]);
		}
	}
}

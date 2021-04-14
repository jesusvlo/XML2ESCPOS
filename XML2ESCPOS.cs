using Microsoft.Language.Xml;
using RawPrint.NetStd;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace XML2ESCPOS
{
	public static class XML2ESCPOS
	{
		static readonly List<TagSimple> simples;
		const char ESC = '\x1b';
		const char GS = '\x1d';
		const string p = "\u0070";
		const string m = "\u0000";
		const string t1 = "\u0025";
		const string t2 = "\u0250";

		static XML2ESCPOS()
		{
			simples = new List<TagSimple>();
			simples.Add(new TagSimple { Tag = "BR", Start = "\n", End = "" });
			simples.Add(new TagSimple { Tag = "LEFT", Start = ESC + "a" + (char)0, End = "" });
			simples.Add(new TagSimple { Tag = "CENTER", Start = ESC + "a" + (char)1, End = "" });
			simples.Add(new TagSimple { Tag = "RIGHT", Start = ESC + "a" + (char)2, End = "" });
			simples.Add(new TagSimple { Tag = "U", Start = ESC + "-" + (char)1, End = ESC + "-" + (char)0 });
			simples.Add(new TagSimple { Tag = "U2", Start = ESC + "-" + (char)2, End = ESC + "-" + (char)0 });
			simples.Add(new TagSimple { Tag = "BOLD", Start = ESC + "E" + (char)1, End = ESC + "E" + (char)0 });
			simples.Add(new TagSimple { Tag = "TAB", Start = "\x09", End = "" });
			simples.Add(new TagSimple { Tag = "RESET", Start = ESC + "@", End = "" });
			simples.Add(new TagSimple { Tag = "END", Start = GS + "V\x41\0", End = "" });
		}

		public static void OpenDrawer(string ImpresoraOrIP, bool EsIP = true)
        {
			string openTillCommand = ESC + p + m + t1 + t2;
			Print(ImpresoraOrIP, openTillCommand, EsIP, "OpenDrawer");
		}

		public static void Print(string PrinterOrIP, string Plantilla, bool EsIP = true, string NombreTrabajo = "Printer Job", int CodePage = 858, List<KeyValuePair<string, string>> Vars = null, List<KeyValuePair<string, object>> BucleVars = null)
		{
			//Compruebo que las variables de bucle son todas ienumerables
			if (BucleVars != null)
				foreach (KeyValuePair<string, object> item in BucleVars)
					if (item.Value as IEnumerable == null)
						return;
			XmlDocumentSyntax parser = Parser.ParseText(Plantilla);
			XmlElementSyntax root = parser.Root as XmlElementSyntax;
			string Result = "";
			BitArray PrintMode = new BitArray(8);
			List<KeyValuePair<string, int>> imagenes = new List<KeyValuePair<string, int>>();
			Dictionary<string, object> varsBucleActivas = new Dictionary<string, object>();
			if (root != null)
			{
				Recursivo(root, ref Result, ref PrintMode, Vars, ref imagenes, ref varsBucleActivas, BucleVars);
			}
			Encoding utf8 = Encoding.UTF8;
			CodePagesEncodingProvider.Instance.GetEncoding(CodePage);
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			Encoding encoding = Encoding.GetEncoding(CodePage);

			string[] trozos = Result.Split("<IMAGE>");
			List<byte> convertedBytes = new List<byte>();
			bool primero = true;
			int index = 0;
			foreach (string trozo in trozos)
			{
				if (trozos.Count() > 1 & !primero)
				{
					convertedBytes.AddRange(Tools.GetImageBytes(imagenes[index].Key, imagenes[index].Value).ToList());
					index++;
				}
				byte[] utfBytes = Encoding.UTF8.GetBytes(trozo);
				convertedBytes.AddRange(Encoding.Convert(utf8, encoding, utfBytes).ToList());
				primero = false;
			}
			byte[] final = convertedBytes.ToArray();
			if (!EsIP)
			{
				try
				{
					new Thread(() =>
					{
						Thread.CurrentThread.IsBackground = true;

						IPrinter Iprinter = new Printer();
						Iprinter.PrintRawStream(PrinterOrIP, new MemoryStream(final), NombreTrabajo);
					}).Start();
				}
				catch
				{
					// Exception here could mean difficulties in connecting to the printer etc
					//                await DisplayAlert("Error", $"Failed to print redemption slip\nReason: {ex.Message}", "OK");
				}
			}
			else
			{
				Socket clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				clientSock.NoDelay = true;
				IPAddress ip = IPAddress.Parse(PrinterOrIP);
				IPEndPoint remoteEP = new IPEndPoint(ip, 9100);
				clientSock.Connect(remoteEP);
				clientSock.Send(final);
				clientSock.Close();
			}
		}

		static void Recursivo(XmlElementSyntax item, ref string texto, ref BitArray PrintMode, List<KeyValuePair<string, string>> valores, ref List<KeyValuePair<string, int>> imagenes,
			ref Dictionary<string, object> varsBucleActivas, List<KeyValuePair<string, object>> BucleVars = null)
		{
			foreach (var elem in item.Content)
			{
				switch (elem.GetType().Name)
				{
					case "XmlTextSyntax":
						texto += (elem as XmlTextSyntax).ToFullString();
						break;
					case "XmlElementSyntax":
						XmlElementSyntax thisElemSyntax = elem as XmlElementSyntax;
						if (thisElemSyntax.Name != "IFNOTNULL" & thisElemSyntax.Name != "LOOP")
						{
							texto += GetIns(thisElemSyntax.Name, true, ref PrintMode);
							Recursivo(thisElemSyntax, ref texto, ref PrintMode, valores, ref imagenes, ref varsBucleActivas, BucleVars);
							texto += GetIns(thisElemSyntax.Name, false, ref PrintMode);
						}
						else
						{
							if (thisElemSyntax.StartTag.AttributesNode.Any())
							{
								if (thisElemSyntax.StartTag.AttributesNode.First().Name == "VARNAME")
								{
									string varName = thisElemSyntax.StartTag.AttributesNode.First().Value;
									if (thisElemSyntax.Name == "IFNOTNULL")
									{
										IEnumerable<KeyValuePair<string, string>> lv1 = valores.Where(x => x.Key == varName);
										if (lv1.Any())
										{
											if (!string.IsNullOrWhiteSpace(lv1.First().Value))
												Recursivo(thisElemSyntax, ref texto, ref PrintMode, valores, ref imagenes, ref varsBucleActivas, BucleVars);
										}
									}
									else //Entonces es LOOP
									{
										IEnumerable<KeyValuePair<string, object>> lv2 = BucleVars.Where(x => x.Key == varName);
										IEnumerable<KeyValuePair<string, object>> ln = varsBucleActivas.Where(x => x.Key == varName);
										if (lv2.Any() & !ln.Any())
										{
											varsBucleActivas.Add(varName, "");
											foreach (object itemBucle in lv2.First().Value as IEnumerable)
											{
												varsBucleActivas[varName] = itemBucle;
												Recursivo(thisElemSyntax, ref texto, ref PrintMode, valores, ref imagenes, ref varsBucleActivas, BucleVars);
											}
										}
										else
											Recursivo(thisElemSyntax, ref texto, ref PrintMode, valores, ref imagenes, ref varsBucleActivas, BucleVars);
									}
								}
							}
						}
						break;
					case "XmlEmptyElementSyntax":
						XmlEmptyElementSyntax thisElemEmptySyntax = elem as XmlEmptyElementSyntax;
						switch (thisElemEmptySyntax.Name)
						{
							case "IMAGE":
								if (thisElemEmptySyntax.Name == "IMAGE")
								{
									if (thisElemEmptySyntax.AttributesNode.Count() == 2)
										if (thisElemEmptySyntax.AttributesNode[0].Name == "PATH" & thisElemEmptySyntax.AttributesNode[1].Name == "F")
										{
											string imgPath = Path.Combine(Directory.GetCurrentDirectory(), thisElemEmptySyntax.AttributesNode[0].Value);
											imagenes.Add(new KeyValuePair<string, int>(imgPath, Convert.ToInt32(thisElemEmptySyntax.AttributesNode[1].Value)));
											texto += "<IMAGE>";
										}
								}
								break;
							case "DEFTABS":
								if (thisElemEmptySyntax.AttributesNode.Any())
								{
									texto += ESC + "D";
									foreach (var n in thisElemEmptySyntax.AttributesNode)
										texto += (char)Convert.ToInt32(n.Value);
									texto += (char)0;
								}
								break;
							default:
								texto += GetIns(thisElemEmptySyntax.Name, true, ref PrintMode);
								break;
						}
						break;
					case "XmlCDataSectionSyntax":
						string var = (elem as XmlCDataSectionSyntax).Value;
						IEnumerable<KeyValuePair<string, string>> lv = valores.Where(x => x.Key == var);
						if (lv.Any())
							texto += lv.First().Value;
						if (var.Contains("."))
						{
							string nomVar = var.Substring(0, var.IndexOf('.'));
							object obj = null;
							if (varsBucleActivas.TryGetValue(nomVar, out obj))
							{
								string propVar = var.Substring(var.IndexOf('.') + 1);
								Type t = obj.GetType();
								PropertyInfo pro = t.GetProperty(propVar);
								texto += pro.GetValue(obj);
							}
						}
						break;
				}
			}
		}

		static string GetIns(string Name, bool Inicial, ref BitArray PrintMode)
		{
			string Result = "";
			IEnumerable<TagSimple> lt = simples.Where(x => x.Tag == Name);
			if (lt.Any())
			{
				if (Inicial)
					Result = lt.First().Start;
				else
					Result = lt.First().End;
			}
			switch (Name)
			{
				case "DWIDTH":
					if (Inicial)
						PrintMode[5] = true;
					else
						PrintMode[5] = false;
					Result = ESC + "!" + (char)Tools.ToNumeral(PrintMode);
					break;
				case "DHEIGHT":
					if (Inicial)
						PrintMode[4] = true;
					else
						PrintMode[4] = false;
					Result = ESC + "!" + (char)Tools.ToNumeral(PrintMode);
					break;
			}
			return Result;
		}
	}
}

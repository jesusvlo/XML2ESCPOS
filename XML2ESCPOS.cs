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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XML2ESCPOS
{
	public class Engine
	{
		readonly List<TagSimple> simples;
		const char ESC = '\x1b';
		const char GS = '\x1d';
		const string p = "\u0070";
		const string m = "\u0000";
		const string t1 = "\u0025";
		const string t2 = "\u0250";

		private string _printerName;
		public string PrinterName 
		{ 
			get => _printerName;
			set
			{
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					throw new InvalidOperationException("Only valid on Windows");
	            else
                {
					_printerName = value;
					_printerIP = null;
                }
			}
		}
		public string PrinterIP 
		{
			get => _printerIP.ToString();
			set 
			{
				bool ValidateIP = IPAddress.TryParse(value, out _printerIP);
				if (!ValidateIP)
					throw new InvalidOperationException("Invalid IP Address");
				else
					PrinterName = null;
			} 
		}
		private IPAddress _printerIP;
		int? PrinterCodePage { get; set; }
		string PrintJobName { get; set; }
		public string Template { get; set; }
		public List<KeyValuePair<string, string>> Vars { get;  private set; }
		public List<KeyValuePair<string, object>> BucleVars { get; private set; }


		public Engine()
		{
			simples = new List<TagSimple>();
			simples.Add(new TagSimple { Tag = "BR", Start = "\n", End = "" });
			simples.Add(new TagSimple { Tag = "LEFT", Start = ESC + "a" + (char)0, End = "" });
			simples.Add(new TagSimple { Tag = "CENTER", Start = ESC + "a" + (char)1, End = "" });
			simples.Add(new TagSimple { Tag = "RIGHT", Start = ESC + "a" + (char)2, End = "" });
			simples.Add(new TagSimple { Tag = "U", Start = ESC + "-" + (char)1, End = ESC + "-" + (char)0 });
			simples.Add(new TagSimple { Tag = "U2", Start = ESC + "-" + (char)2, End = ESC + "-" + (char)0 });
			simples.Add(new TagSimple { Tag = "B", Start = ESC + "E" + (char)1, End = ESC + "E" + (char)0 });
			simples.Add(new TagSimple { Tag = "TAB", Start = "\x09", End = "" });
			simples.Add(new TagSimple { Tag = "RESET", Start = ESC + "@", End = "" });
			simples.Add(new TagSimple { Tag = "END", Start = "\n" + GS + "V\x41\0", End = "" });
			if (Template == null)
				Template = "";
			if (PrinterCodePage == null)
				PrinterCodePage = 858;

			Vars = new List<KeyValuePair<string, string>>();
			BucleVars = new List<KeyValuePair<string, object>>();
		}

		byte[] EncodeToBytes(string Info)
        {
			int CP = 858;
			if (PrinterCodePage != null)
				CP = PrinterCodePage ?? default(int);
			Encoding utf8 = Encoding.UTF8;
			CodePagesEncodingProvider.Instance.GetEncoding(CP);
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			Encoding encoding = Encoding.GetEncoding(CP);
			return Encoding.UTF8.GetBytes(Info);
		}

		void DirectPrint(byte[] bytes, string PrintJobNamePar)
		{
			if (!string.IsNullOrWhiteSpace(_printerName) | _printerIP != null)
				try
				{
					if (!string.IsNullOrWhiteSpace(_printerName))
					{
						IPrinter Iprinter = new Printer();
						Iprinter.PrintRawStream(_printerName, new MemoryStream(bytes), PrintJobNamePar);
					}
					else
					{
						Socket clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
						clientSock.NoDelay = true;
						IPEndPoint remoteEP = new IPEndPoint(_printerIP, 9100);
						clientSock.Connect(remoteEP);
						clientSock.Send(bytes);
						clientSock.Close();
					}
				}
				catch
				{
					// Exception here could mean difficulties in connecting to the printer etc
					//                await DisplayAlert("Error", $"Failed to print redemption slip\nReason: {ex.Message}", "OK");
				}
			else
				throw new InvalidOperationException("Invalid Printer");
		}

		public void OpenDrawer()
		{
			DirectPrint(EncodeToBytes(ESC + p + m + t1 + t2), "OpenDrawer");
		}

		public void Print()
		{
			//Compruebo que las variables de bucle son todas ienumerables
			if (BucleVars != null)
				foreach (KeyValuePair<string, object> item in BucleVars)
					if (item.Value as IEnumerable == null)
						return;
			XmlDocumentSyntax parser = Parser.ParseText(Template);
			XmlElementSyntax root = parser.Root as XmlElementSyntax;
			string Result = "";
			BitArray PrintMode = new BitArray(8);
			List<KeyValuePair<string, int>> imagenes = new List<KeyValuePair<string, int>>();
			Dictionary<string, object> varsBucleActivas = new Dictionary<string, object>();
			if (root != null)
			{
				Recursivo(root, ref Result, ref PrintMode, ref imagenes, ref varsBucleActivas);
			}

			int CP = 858;
			if (PrinterCodePage != null)
				CP = PrinterCodePage ?? default(int);

			Encoding utf8 = Encoding.UTF8;
			CodePagesEncodingProvider.Instance.GetEncoding(CP);
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			Encoding encoding = Encoding.GetEncoding(CP);

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

			if (PrintJobName == null)
				PrintJobName = "PrintJob";
			DirectPrint(final, PrintJobName);
		}

		void Recursivo(XmlElementSyntax item, ref string texto, ref BitArray PrintMode, ref List<KeyValuePair<string, int>> imagenes, ref Dictionary<string, object> varsBucleActivas)
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
						if (thisElemSyntax.Name != "IFNOTNULL" & thisElemSyntax.Name != "FOREACH")
						{
							texto += GetIns(thisElemSyntax.Name, true, ref PrintMode);
							Recursivo(thisElemSyntax, ref texto, ref PrintMode, ref imagenes, ref varsBucleActivas);
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
										IEnumerable<KeyValuePair<string, string>> lv1 = Vars.Where(x => x.Key == varName);
										if (lv1.Any())
										{
											if (!string.IsNullOrWhiteSpace(lv1.First().Value))
												Recursivo(thisElemSyntax, ref texto, ref PrintMode, ref imagenes, ref varsBucleActivas);
										}
									}
									else //Entonces es FOREACH
									{
										if (!varName.Contains('.'))
										{
											IEnumerable<KeyValuePair<string, object>> lv2 = BucleVars.Where(x => x.Key == varName);
											IEnumerable<KeyValuePair<string, object>> ln = varsBucleActivas.Where(x => x.Key == varName);
											if (lv2.Any() & !ln.Any())
											{
												varsBucleActivas.Add(varName, "");
												foreach (object itemBucle in lv2.First().Value as IEnumerable)
												{
													varsBucleActivas[varName] = itemBucle;
													Recursivo(thisElemSyntax, ref texto, ref PrintMode, ref imagenes, ref varsBucleActivas);
												}
												varsBucleActivas.Remove(varName);
											}
											else
											{
												Recursivo(thisElemSyntax, ref texto, ref PrintMode, ref imagenes, ref varsBucleActivas);
											}
										}
										else
                                        {
											List<string> partesVarName = varName.Split('.').ToList();
											string clasePadre = string.Join('.', partesVarName.GetRange(0, partesVarName.Count - 1));
											IEnumerable<KeyValuePair<string,object>> lvar = varsBucleActivas.Where(x => x.Key == clasePadre);
											if (lvar.Any())
											{
												Type t = lvar.First().Value.GetType();
												PropertyInfo pro = t.GetProperty(partesVarName.Last());
												varsBucleActivas.Add(varName, "");
												if (pro.GetValue(lvar.First().Value) != null)
													foreach (object itemBucle in pro.GetValue(lvar.First().Value) as IEnumerable)
													{
														varsBucleActivas[varName] = itemBucle;
														Recursivo(thisElemSyntax, ref texto, ref PrintMode, ref imagenes, ref varsBucleActivas);
													}
												varsBucleActivas.Remove(varName);
											}
											else
											{
												Recursivo(thisElemSyntax, ref texto, ref PrintMode, ref imagenes, ref varsBucleActivas);
											}
										}
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
						IEnumerable<KeyValuePair<string, string>> lv = Vars.Where(x => x.Key == var);
						if (lv.Any())
							texto += lv.First().Value;
						if (var.Contains("."))
						{
							List<string> partesVarName = var.Split('.').ToList();

							string nomVar = var.Substring(0, var.Length-(partesVarName.Last().Length + 1));
							object obj = null;
							if (varsBucleActivas.TryGetValue(nomVar, out obj))
							{
								string propVar = partesVarName.Last();
								Type t = obj.GetType();
								PropertyInfo pro = t.GetProperty(propVar);
								texto += pro.GetValue(obj);
							}
						}
						break;
				}
			}
		}

		string GetIns(string Name, bool Inicial, ref BitArray PrintMode)
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

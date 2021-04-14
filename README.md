XML2ESCPOS
==========

Net Standard library that provides a simple XML Template engine with support for images, variables and loops. 
For ESC / POS Thermal Printers. 
You can print to Windows printers (using the print queue so windows takes care of waiting for it to turn on or have paper) or directly to network printers. 

Example:

	string plantilla = "<C><RESET/><CENTER/><B>Hello!</B><BR><BR><END/></C>";

	XML2ESCPOS.XML2ESCPOS.Print("Epson", plantilla, false);

Printer print:

            **Hello!**

#### Usage:

**Print(PrinterOrIP, Plantilla, EsIP, NombreTrabajo, CodePage, Vars, BucleVars)**
		
ImpresoraOrIP: (Required String) Windows Printer Name if EsIP = false or IP Address if EsIP = true

Plantilla: (Required String) String with the Template

EsIP: (Optional Bool, Default is True) If PrinterOrIp is System Windows Printer Name you must set to false.
If PrinterOrIP is Ip Address you can ignore it or you can set to True

NombreTrabajo: (Optional String, Default is "Printer Job") Print job name

CodePage: (Optional Int, Default is 858) Codepage that has the thermal printer configured.
https://docs.microsoft.com/en-us/windows/win32/intl/code-page-identifiers

Vars: (Opcional List<KeyValuePair<string, string>>) List of the variables that you are going to use with their name and value. [More Info](#Vars)

BubleVars: (Opcional IEnumerable<KeyValuePair<string, string>>) List of variables that are the lists that you will use with each loop with its name [More Info](#BucleVars)

#### Vars:
Example:

	string plantilla = "<C><RESET/><LEFT/><B>Hello <![CDATA[MyName]]>!</B><BR><BR><END/></C>";
	
	List<KeyValuePair<string, string>> valores = new List<KeyValuePair<string, string>>();
	
	valores.Add(new KeyValuePair<string, string>("MyName", "Jesus"));
	
	XML2ESCPOS.XML2ESCPOS.Print("Epson", plantilla, false, Vars:valores);

Printer print:

**Hello Jesus!**

#### BucleVars:
Example:

	public class Client
    {
        public string Name { get; set; }
    }

	string plantilla = "<C><RESET/><LEFT/><LOOP VARNAME='clientes'><B>Hello <![CDATA[cliente.Name]]>!</B><BR></LOOP><BR><END/></C>";

	List<Client> lc = new List<Client>();
	lc.Add(new ClienteT { Name = "Jesus" });
	lc.Add(new ClienteT { Name = "Juan" });

	List<KeyValuePair<string,object>> valoresBucle = new List<KeyValuePair<string,object>>();
	KeyValuePair<string,object> clientevar = new KeyValuePair<string,object>("cliente", lc);
	valoresBucle.Add(clientevar);

	XML2ESCPOS.XML2ESCPOS.Print("Epson", plantilla, false, BucleVars:valoresBucle);

Printer print:

**Hello Jesus!**
**Hello Juan!**
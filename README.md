XML2ESCPOS
==========

Net Standard library that provides a simple XML Template engine with support for images, variables and loops. 
For ESC / POS Thermal Printers. 
You can print to Windows printers (using the print queue so windows takes care of waiting for it to turn on or have paper) or directly to network printers. 

Example:

    XML2ESCPOS.Engine xML2ESCPOS = new XML2ESCPOS.Engine
    {
         Template = "<C><RESET/><CENTER/>Hello!<BR><BR><END/></C>",
         PrinterName = "Epson"
    };
    xML2ESCPOS.Print();

Printer print:

            Hello!



#### Engine Properties:

PrinterName: Windows Printer Name. When PrinterName is set, PrinterIP is disabled 

PrinterIP: Printer IP Address. When PrinterIP is set, PrinterName is disabled

Plantilla: String with the Template

PrintJobName: (Default is "PrintJob") Print job name

CodePage: (Default is 858) Codepage that has the thermal printer configured.
https://docs.microsoft.com/en-us/windows/win32/intl/code-page-identifiers

Vars: (List<KeyValuePair<string, string>>) List of the variables that you are going to use with their name and value. [More Info](#Vars)

BubleVars: (List<KeyValuePair<string, string>>) List of variables that are the lists that you will use with each loop with its name [More Info](#BucleVars)

#### XML Codes:

\<RESET/> Initialize printer

\<LEFT/> Left justification. This command is enabled only when processed at the beginning of the line. Settings of this command are effective until \<RIGHT/> or \<CENTER/> or \<RESET/> or power off

\<CENTER/> Left justification. This command is enabled only when processed at the beginning of the line. Settings of this command are effective until \<LEFT/> or \<CENTER/> or \<RESET/> or power off

\<RIGHT/> Left justification. This command is enabled only when processed at the beginning of the line. Settings of this command are effective until \<LEFT/> or \<RIGHT/> or \<RESET/> or power off

\<END/> Feed and cut paper

\<BR/> or \<BR> New Line

\<TAB/> or \<TAB> Next Tab

\<B> \</B> Emphasized Text

\<U> \</U> Underline Text

\<U2> \</U2> Underline 2-points Text

\<DHEIGHT> \</DHEIGHT> Double Height Text

\<DWIDTH> \</DWIDTH> Double Width Text

\<IMAGE PATH='path' F='f'/> Print Image. PATH: path relative to current directory.
F: size factor (integer), depent on printer model.

Example in Template: \<IMAGE PATH='wwwroot\\img\\logo.jpg' F='200'/>


\<DEFTABS N1='n1' N2='n2' .../> Set horizontal tab positions. Settings of this command are effective until another \<DEFTABS/> or\<RESET/> or power off

Example in Template: \<DEFTABS N1='2' N2='15' N3='25'/>

\<IFNOTNULL VARNAME='varname'> \</IFNOTNULL> Print the inside only if the variable is not null or empty or only white spaces 

Example:

	string plantilla = "<C><RESET/><LEFT/><IFNOTNULL VARNAME='MyName'><B>Hello <![CDATA[MyName]]>!</B></IFNOTNULL><BR>" +

		"<IFNOTNULL VARNAME='MyName'><B>Hello <![CDATA[YourName]]>!</B></IFNOTNULL><BR><END/></C>";
	
	XML2ESCPOS.Engine xML2ESCPOS = new XML2ESCPOS.Engine
            {
                Template = plantilla,
                PrinterName = "Epson"
            };

    xML2ESCPOS.Vars.Add(new KeyValuePair<string, string>("MyName", "Jesus"));
    xML2ESCPOS.Vars.Add(new KeyValuePair<string, string>("YourName", "  "));

	xML2ESCPOS.Print();

Printer Print:

**Hello Jesus!**




#### Vars:
Example:

    XML2ESCPOS.Engine xML2ESCPOS = new XML2ESCPOS.Engine
        {
            Template = "<C><RESET/><LEFT/><B>Hello <![CDATA[MyName]]>!</B><BR><BR><END/></C>",
            PrinterName = "Epson"
        };
    xML2ESCPOS.Vars.Add(new KeyValuePair<string, string>("MyName", "Jesus"));
    xML2ESCPOS.Print();

Printer print:

**Hello Jesus!**

#### BucleVars:
Example:

	public class Client
    {
        public string Name { get; set; }
    }

	XML2ESCPOS.Engine xML2ESCPOS = new XML2ESCPOS.Engine
            {
                Template = "<C><RESET/><LEFT/><FOREACH VARNAME='clientes'><B>Hello <![CDATA[clientes.Name]]>!</B><BR></FOREACH><BR><END/></C>",
                PrinterName = "Epson"
            };

    List<Client> lc = new List<Client>();
    lc.Add(new Client { Name = "Jesus" });
    lc.Add(new Client { Name = "Juan" });
    
    xML2ESCPOS.BucleVars.Add(new KeyValuePair<string, object>("clientes", lc));
    xML2ESCPOS.Print();

Printer print:

**Hello Jesus!**

**Hello Juan!**
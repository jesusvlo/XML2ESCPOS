XML2ESCPOS
==========

Net Standard library that provides a simple XML Template engine with support for images, variables and loops. 
For ESC / POS Thermal Printers. 
You can print to Windows printers (using the print queue so windows takes care of waiting for it to turn on or have paper) or directly to network printers. 

QuickStart

string Template = "\<TEMPLATE\>\<RESET/\>\<IMAGE PATH='wwwroot\\img\\logo.jpg' F='200'/\>\<LEFT/\>\<B\>Factura Nº: \<![CDATA[NumFact]]\>;				

List<KeyValuePair<string, string>> valores = new List<KeyValuePair<string, string>>();
valores.Add(new KeyValuePair<string, string>("NumFact", "1234546"));

XML2ESCPOS.XML2ESCPOS.Imprimir("bebida", Template, "Ticket", EsIP:false, Vars: valores);



"bebida" is the windows printer name
"Ticket" is the printer job name

XML2ESCPOS
==========

Net Standard library that provides a simple XML Template engine with support for images, variables and loops. 
For ESC / POS Thermal Printers. 
You can print to Windows printers (using the print queue so windows takes care of waiting for it to turn on or have paper) or directly to network printers. 

Example:

	string plantilla = "<C><RESET/><CENTER/><B>Hello!<B/><BR><BR><END/><C/>";

	XML2ESCPOS.XML2ESCPOS.Imprimir("Epson", plantilla, "Ejemplo", EsIP:false);

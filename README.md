# DS2POCO
WCF Data Service to POCO classes asyncronous code generator using [Roslyn](https://github.com/dotnet/roslyn) compiler platform

This is a C# class library that utilizes the brand new Roslyn compiler platform to process the metadata information of a given WCF Data Service and to generate pure POCO (Plain Old CLR Object) classes.

It uses .Net 4.5.

## Steps

* Download the project and compile it
  * It will download packages according to the `packages.config` file
* Add the project (or the compiled dll) to your referencing project
  * The referencing project also needs to have the Microsoft.CodeAnalysis package!  
* Call the `Processing` function with the required parameters

## Example
### Parameters
* **uri**: URI of DataService metadata (required).
* **exportDirectory**: Directory for the output files (required).
* **primaryNamespace**: Basic namespace (default value: Proxy).
* **baseClassName**: Base class name (optional).
* **usingNamespaces**: Using namespaces (optional).
* **callback**: Callback delegate (optional).
   

```csharp


public async void CallDSProcessing()
  {
    string uriOfDataService = "http://services.odata.org/Northwind/Northwind.svc/$metadata" //notice the $metadata query parameter
    string exportDirectory = @"c:\temp\DS2POCO\";
    string namespace = "MyProject.Model";
    string baseClassName = "AbstractModel";
    string usingNamespaces = "System.IO";
    if (!string.IsNullOrEmpty(uriOfDataService) && !string.IsNullOrEmpty(exportDirectory))
      await DS2POCO.DS2POCOProcessor.Processing(uriOfDataService, exportDirectory, namespace, baseClassName, usingNamespaces, 
        (str) => Dispatcher.Invoke(() => results.Text += str + Environment.NewLine));
    else
      results.Text += "Required fields are missing" + Environment.NewLine;
  }
  ```




 


# az-aas-paas-automation
 This GitHub repository contains a code sample that demonstrates how you might secure the automation of Azure Analysis services data refresh actions. 

## General Flow of Function Logic
This function will perform the following actions:
1.  Attempt connection to Azure SQL Server. This attempt is expected to fail as the function IP address will not be in the SQL server whitelist.
1. Retrieve Function IP address from SQL Server connection error message.
1. Whitelist Function IP
1. Create a new Login and User within the Azure SQL Server. This login will be used by Azure Analysis Services to connect during cube processing
1. Connect to Azure Analysis services and update the datasource's connection string with the new username and password.
1. Attempt to process cube. This attempt is expected to fail as the Analysis Servcies IP address is not whitelisted in the Azure SQL server's firewall. 
1. Retrieve Azure Analysis Services IP address from cube refresh error message.
1. Whitelist Azure Analysis Servcies IP.
1. Process Cube.
1. Remove Analysis Services User and Login from SQL Server.
1. Remove entries from SQL Server Firewall.

## Additional Considerations / Potential Enhancements
- Move from Service Principal to MSI to update datasource within Azure Analysis Services database. This is currently set to use the Service Principal but it may be possible to move this to the MSI. 
- Add logic to auto-whitelist the Function App IP in the Azure Analysis Services instance. Currently function applies auto-white-listing functionalty to the Azure SQL Server instance but not the Azure Analysis Services instance.
- Change SQL Server P/W reset function to account for default Azure SQL password complexity requirements. At the moment the code will occassionally fail due to password complexity constraints.
- Long runnning cube processing considerations. The AZ function currently processes the cube in a synchronous manner. For cubes with long processing times consider configuring the function to allow for long running operations (may need an ASE) or move to an asyncronous processing pattern (possible via rest api for Azure Analysis Services)

## Instructions
These instructions assume that you already have an existing Azure Function App Created. 

- Over-write the code in your run.csx and your project.json files with the relevant code contained in this repository (see run.csx and project.json) in the AzureFunction folder.
- Upload the Analysis services AMO / TMO assemblies to the bin folder of your function. See [this link](https://azure.microsoft.com/en-au/blog/automating-azure-analysis-services-processing-with-azure-functions/) for a guide. Note that the exact location of the dlls on your system may vary depending on versions (I recommend version 15 which for me was in C:\Program Files\Microsoft SQL Server\150\SDK\Assemblies). Also note that you will require three dll's not two (Microsoft.AnalysisServices.Tabular.dll,Microsoft.AnalysisServices.Core.dll,Microsoft.AnalysisServices.dll).
- Make sure that you have granted your function app MSI and Service Principal the appropriate credentials on both the Azure SQL instance and the Azure Analysis services instance.

You will need to create the following application settings in the function app (replace the items in curly brackets with your information):

```json
{
"AASRegion": "{Region in which your Azure Analysis Services Server is deployed}",
"AASServerName": "{Name of your Azure Analysis Services Instance}",
"ApplicationId": "{This is the Application Id of service principal}",
"AuthenticationKey": "{This is the Authentication Key used by your service principal}",
"MsiId": "{This is the pincipalid associated with your Azure Function's MSI}",
"OLAPReaderAccountName": "OLAPReaderAccount",
"ResourceGroup": "{this is the resource group for your azure resources}",
"SQLDatabase": "{this is the name of the SQL database that you will be getting the data from}",
"SQLServerID": "{this is the name of the SQL Server that you will be getting the data from}",
"Subscription": "{this is your azure subscription guid}",
"TenantId": "{this is your azure tenant guid}",
"UseMSIForSQL": "true",
"UseMSIForOLAP": "false"
}
```

## Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## License
These samples and templates are all licensed under the MIT license. See the license.txt file in the root.


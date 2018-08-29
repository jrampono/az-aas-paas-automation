# az-aas-paas-automation
 This GitHub repository contains a code sample that demonstrates how you might secure the automation of Azure Analysis services data refresh actions. 

## Instructions
These instuctions assume that you already have an existing Azure Function App Created. 

(1) Over-write the code in your run.csx and your project.json files with the relevant code contained in this repository (see run.csx and project.json) in the AzureFunction folder.\
(2) Upload the Analysis services AMO / TMO assemblies to the bin folder of your function. See [this link](https://azure.microsoft.com/en-au/blog/automating-azure-analysis-services-processing-with-azure-functions/) for a guide. Note that the exact location of the dlls on your system may vary depending on versions (I recommend version 15 which for me was in C:\Program Files\Microsoft SQL Server\150\SDK\Assemblies). Also note that you will require three dll's not two (Microsoft.AnalysisServices.Tabular.dll,Microsoft.AnalysisServices.Core.dll,Microsoft.AnalysisServices.dll).\
(3) Make sure that you have granted your function app MSI and Service Principal the appropriate credentials on both the Azure SQL instance and the Azure Analysis services instance.\

You will need to create the following application settings in the function app (replace the items in curly brackets with your information):

    "AASConnectionString": "Provider=MSOLAP;Data Source=asazure://{your azure analysis services region}.asazure.windows.net/{your Azure analysis services servername};Password=/*PW*/;Persist Security Info=True;Impersonation Level=Impersonate",
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
    "UseMSIForOLAP": "false",

## Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## License
These samples and templates are all licensed under the MIT license. See the license.txt file in the root.


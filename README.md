# az-aas-paas-automation
 This GitHub repository contains a code sample that demonstrates how you might secure the automation of Azure Analysis services data refresh actions. 

## General Flow of Function Logic
This function will perform the following actions:\
a) Attempt connection to Azure SQL Server: This attempt is expected to fail as the function IP address will not be in the SQL server whitelist.\
b) Retrieve Function IP address from SQL Serevr connection error message.\
c) Whitelist Function IP\
d) Create a new Login and User within the Azure SQL Server. This login will be used by Azure Analysis Services to connect during cube processing\
e) Connect to Azure Analysis services and update the datasource's connection string with the new username and password.\
f) Attempt to process cube. This attempt is expected to fail as the Analysis Servcies IP address is not whitelisted in the Azure SQL server's firewall. 
g) Retrieve Azure Analysis Services IP address from cube refresh error message.\
h) Whitelist Azure Analysis Servcies IP.\
i) Process Cube.\
j) Remove Analysis Services User and Login from SQL Server.\
j) Remove entries from SQL Server Firewall.\

## Instructions
These instructions assume that you already have an existing Azure Function App Created. 

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


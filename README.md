# az-aas-paas-automation
 This GitHub repository contains a code sample that demonstrates how you might secure the automation of Azure Analysis services data refresh actions. 

## Instructions
These instuctions assume that you already have an existing Azure Function App Created. 

(1) Over-write the code in your run.csx and your project.json files with the relevant code contained in this repository (see run.csx and project.json) in the AzureFunction folder.\
(2) Upload the Analysis services AMO / TMO assemblies to the bin folder of your function. See [this link](https://azure.microsoft.com/en-au/blog/automating-azure-analysis-services-processing-with-azure-functions/) for a guide. Note that the exact location of the dlls on your system may vary depending on versions.\
(3) Make sure that you have granted your function app MSI and Service Principal the appropriate credentials on both the Azure SQL instance and the Azure Analysis services instance.\

## Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## License
These samples and templates are all licensed under the MIT license. See the license.txt file in the root.

## Questions
Email questions to: sqlserversamples@microsoft.com.

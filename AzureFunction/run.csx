#r "System.Configuration"
#r "System.Data"
#r "System.Text.RegularExpressions"
#r "D:\home\site\wwwroot\HttpTriggerCSharp1\bin\Microsoft.AnalysisServices.Core.dll" 
#r "D:\home\site\wwwroot\HttpTriggerCSharp1\bin\Microsoft.AnalysisServices.Tabular.dll" 


using System.Net;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Management.Sql.Fluent;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using PasswordUtility.PasswordGenerator;



public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
  //Publish Code Bellow this point
        {

            log.Info("C# HTTP trigger function processed a request.");
            try
            {
                string AzFunctionInternalIP = "";

                //Get Access Token for Database REST API operations
                string SQLAccessToken = Helpers.GetAzureRestApiToken("https://database.windows.net/", Helpers.GlobalConfigs.UseMSIForSQL);
                //Get Access Token for OLAP REST API operations
                string OLAPAccessToken = Helpers.GetAzureRestApiToken("https://australiasoutheast.asazure.windows.net/", Helpers.GlobalConfigs.UseMSIForOLAP);

              
                //Get MSI for C# API operations
                var msiCred = Helpers.GetAzureCreds(Helpers.GlobalConfigs.UseMSIForSQL);
                var azureAuth = Azure.Configure().WithLogLevel(HttpLoggingDelegatingHandler.Level.BodyAndHeaders).Authenticate(msiCred);
                var azure = azureAuth.WithSubscription(Helpers.GlobalConfigs.Subscription);
                log.Info("Selected subscription: " + azure.SubscriptionId);

                //Get The Target SQL Server
                var SQLServer = azure.SqlServers.Manager.SqlServers.GetById(string.Format("/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Sql/servers/{2}", Helpers.GlobalConfigs.Subscription, Helpers.GlobalConfigs.ResourceGroup, Helpers.GlobalConfigs.SQLServerID));
                string SQLAdminUsername = SQLServer.AdministratorLogin;
                //Set SQL Admin Password for Target SQL Server
                var SQLAdminPassword = PwGenerator.Generate(15, true, true, false).ReadString();
                
                //Create Helper Class For SQL Actions
                SQLActions sqlactions = new SQLActions(SQLAdminUsername, SQLAdminPassword);
                sqlactions.log = log;
                sqlactions.SQLServer = SQLServer;

                //ResetSQLPassword
                sqlactions.ResetAdminPW(SQLAdminPassword);

                //Create a Cleanup Object to Assist with Cleanup
                CleanUp CU = new CleanUp();
                CU.sqlactions = sqlactions;

                try
                {
                    sqlactions.WhiteListAutomationFunctionIP();
                    sqlactions.AddLoginAndUserForAutomationAccount();

                    SSASActions ssas = new SSASActions();
                    ssas.log = log;
                    ssas.OLAPAccessToken = OLAPAccessToken;
                    ssas.ConnectToSSAS();
                    ssas.SetDataSourceConnectionString(SQLServer, SQLAdminUsername, SQLAdminPassword);
                    log.Info("Cube Process Complete");

                }
                catch (System.Exception ExceptionRequiresCleanup)
                {
                    log.Error("Exception caught that requires cleanup:" + Newtonsoft.Json.JsonConvert.SerializeObject(ExceptionRequiresCleanup).ToString());
                    throw ExceptionRequiresCleanup;
                }
                finally
                {
                    log.Info("Beginning cleanup");
                    CU.RunCleanUpActions(log);

                }
          
                return req.CreateResponse(HttpStatusCode.OK, "Success");

            }
            catch (System.Exception e)
            {
                log.Error("Function Failed" + Newtonsoft.Json.JsonConvert.SerializeObject(e).ToString());
                return req.CreateResponse(HttpStatusCode.BadRequest, e);
            }

        }

        public class CleanUp
        {

            public SQLActions sqlactions { get; set; }

            public void Cleanup()
            { }

            public void RunCleanUpActions(TraceWriter log)
            {
                using (SqlConnection conn = new SqlConnection(sqlactions.DbConStr))
                {
                    string DBConnectionAttempt = Helpers.TryToOpenSQLConnection(conn);
                    string SQL = string.Format(@"
                    IF EXISTS (SELECT * FROM sys.sysusers WHERE name='{0}')
                    BEGIN 
                        DROP USER {0}
                    END
                    ", Helpers.GlobalConfigs.OLAPReaderAccountName);

                    Helpers.ExecuteSQLStatement(SQL, conn, log, "Attempt to remove user from db", false);
                }

                using (SqlConnection MasterConn = new SqlConnection(sqlactions.MasterConStr))
                {
                    string MasterConnectionAttempt = Helpers.TryToOpenSQLConnection(MasterConn);
                    string SQL2 = string.Format(@"
                    If Exists (
                                    select name from sys.sql_logins where name = '{0}'
                                )
                    BEGIN 
                        DROP LOGIN {0}
                    END
                    ", Helpers.GlobalConfigs.OLAPReaderAccountName);

                    Helpers.ExecuteSQLStatement(SQL2, MasterConn, log, "Attempt to remove login from server", false);
                }

                //Remove Firewallrule
                var ExistingRule = sqlactions.SQLServer.FirewallRules.Get("AutomationFunctionIP");
                if (ExistingRule != null)
                {
                    ExistingRule.Delete();
                    log.Info("Firewall AutomationFunctionIP Whitelist Entry Removed");
                }

                var ExistingRule2 = sqlactions.SQLServer.FirewallRules.Get("SSASCubeIP");
                if (ExistingRule2 != null)
                {
                    ExistingRule2.Delete();
                    log.Info("Firewall SSASCubeIP Whitelist Entry Removed");
                }

            }
        }

        public class SSASActions
        {
            public TraceWriter log {get; set;}
            
            public string OLAPAccessToken { get; set; }

            public Microsoft.AnalysisServices.Tabular.Server asSvr { get; set; }

            public void ConnectToSSAS()
            {
                //Olap Test
                log.Info("Connecting to Azure Analysis Services Server");
                var connStr1 = Helpers.GlobalConfigs.AASConnectionString;
                try
                {
                    this.asSvr = new Microsoft.AnalysisServices.Tabular.Server();
                    this.asSvr.Connect(connStr1.Replace("/*PW*/", OLAPAccessToken));
                }
                catch (System.Exception OlapConnectionError)
                {
                    log.Error(OlapConnectionError.Message);
                }
            }


            public void SetDataSourceConnectionString_MakeConnectionAttempt(ISqlServer SQLServer, string SQLAdminUsername, string SQLAdminPassword)
            {
                log.Info("Setting SSAS Datasource Connection String");
                
                foreach (Microsoft.AnalysisServices.Tabular.Database d in asSvr.Databases)
                {
                    log.Info("Database: " + d.Name);
                }
                var db = asSvr.Databases[0];
                var model = db.Model;
                Microsoft.AnalysisServices.Tabular.ProviderDataSource ds = (Microsoft.AnalysisServices.Tabular.ProviderDataSource)model.DataSources[0];
                ds.ConnectionString = string.Format("Server=tcp:{0}.database.windows.net,1433;Initial Catalog={3};Persist Security Info=False;User ID={1};Password={2};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;", Helpers.GlobalConfigs.SQLServerID, SQLAdminUsername, SQLAdminPassword, Helpers.GlobalConfigs.SQLDatabase);
                db.Update(Microsoft.AnalysisServices.UpdateOptions.ExpandFull);
                log.Info("Processing Cube");
                model.RequestRefresh(Microsoft.AnalysisServices.Tabular.RefreshType.Full);
            }

            public void SetDataSourceConnectionString(ISqlServer SQLServer, string SQLAdminUsername, string SQLAdminPassword)
            {
                
                SetDataSourceConnectionString_MakeConnectionAttempt(SQLServer, SQLAdminUsername, SQLAdminPassword);
                try
                {
                    log.Info("Attempting First SSAS Cube Refresh");
                    asSvr.Databases[0].Update(Microsoft.AnalysisServices.UpdateOptions.ExpandFull);
                }
                catch (Microsoft.AnalysisServices.OperationException SSASRefreshError)
                {
                    SQLActions sqlactions = new SQLActions(SQLAdminUsername, SQLAdminPassword);
                    sqlactions.log = log;
                    sqlactions.SQLServer = SQLServer;
                    sqlactions.AddToFirewallIP(SSASRefreshError.Message, "SSASCubeIP");
                    log.Info("Attempting Second SSAS Cube Refresh");
                    SetDataSourceConnectionString_MakeConnectionAttempt(SQLServer, SQLAdminUsername, SQLAdminPassword);
                    asSvr.Databases[0].Update(Microsoft.AnalysisServices.UpdateOptions.ExpandFull);
                }
                catch (System.Exception)
                {
                    log.Error("Non SSAS Error on Cube Refresh");
                }
                finally
                {
                    asSvr.Disconnect();
                }
            }

        }

        public class SQLActions
        {
            public TraceWriter log { get; set; }
            public ISqlServer SQLServer { get; set; }

            public string DbConStr { get; set; }

            public string MasterConStr { get; set; }

            public SQLActions(string SQLAdminUsername, string SQLAdminPassword)
            {
                this.DbConStr = string.Format("Server=tcp:{0}.database.windows.net,1433;Initial Catalog={3};Persist Security Info=False;User ID={1};Password={2};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;", Helpers.GlobalConfigs.SQLServerID, SQLAdminUsername, SQLAdminPassword, Helpers.GlobalConfigs.SQLDatabase);
                this.MasterConStr = string.Format("Server=tcp:{0}.database.windows.net,1433;Initial Catalog=master;Persist Security Info=False;User ID={1};Password={2};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;", Helpers.GlobalConfigs.SQLServerID, SQLAdminUsername, SQLAdminPassword);
            }

            public void AddToFirewallIP(string RefreshError, string RuleName)
            {
                string SX = "Client with IP address";
                string EX = " is not allowed to access the server";

                if (RefreshError.Contains(SX) && RefreshError.Contains(EX))
                //Add IP Address to Firewall
                {
                    RefreshError = RefreshError.Substring(RefreshError.IndexOf(SX), (RefreshError.IndexOf(EX) + EX.Length) - RefreshError.IndexOf(SX));
                    string AzFunctionInternalIP = RefreshError.Split('\'')[1];
                    log.Info("Function internal IP:" + AzFunctionInternalIP);

                    //WhiteList the Azure Function Address in the SQL Server Firewall
                    var ExistingRule = SQLServer.FirewallRules.Get(RuleName);
                    if (ExistingRule != null)
                    {
                        ExistingRule.Delete();
                    }

                    SQLServer.FirewallRules.Define(RuleName).WithIPAddress(AzFunctionInternalIP).Create();

                }
                else
                {
                    log.Error("Non firewall related SSAS error: " + RefreshError);
                }
            }

            public void AddLoginAndUserForAutomationAccount()
            {
                log.Info("Going to Add Login and User to DB");
                using (SqlConnection conn = new SqlConnection(DbConStr))
                {
                    using (SqlConnection MasterConn = new SqlConnection(MasterConStr))
                    {
                        string DBConnectionAttempt = Helpers.TryToOpenSQLConnection(conn);
                        string MasterConnectionAttempt = Helpers.TryToOpenSQLConnection(MasterConn);



                        log.Info("Adding Login");
                        //Execute SQL Statement to Provision User
                        var password = PwGenerator.Generate(15, true, true, false).ReadString();
                        var SQLStatement = string.Format(@"
                                                        If not Exists (
                                                                        select name from sys.sql_logins where name = '{1}'
                                                                        )
                                                        BEGIN 
                                                            CREATE LOGIN {1} WITH PASSWORD = '{0}'
                                                        END", password, Helpers.GlobalConfigs.OLAPReaderAccountName);
                        Helpers.ExecuteSQLStatement(SQLStatement, MasterConn, log, "OLAPReader Login", true);

                        log.Info("Adding User");
                        //Execute SQL Statement to Provision User
                        SQLStatement = "";
                        SQLStatement = string.Format(@"
                                                        IF NOT EXISTS (SELECT * FROM sys.sysusers WHERE name='{0}')
                                                        BEGIN 
                                                            CREATE USER {0} FOR LOGIN {0} WITH DEFAULT_SCHEMA = dbo
                                                        END
                                                        ", Helpers.GlobalConfigs.OLAPReaderAccountName);
                        Helpers.ExecuteSQLStatement(SQLStatement, conn, log, "OLAPReader User", true);


                    }
                }
            }
               
            public void WhiteListAutomationFunctionIP()
            {
                //Try Initial Connection to Target DB (Using MSI Cred)
                //var str = string.Format("Server=tcp:{0}.database.windows.net,1433;Initial Catalog={1};Persist Security Info=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;", Helpers.GlobalConfigs.SQLServerID, Helpers.GlobalConfigs.SQLDatabase);
                

                using (SqlConnection conn = new SqlConnection(DbConStr))
                {
                    using (SqlConnection MasterConn = new SqlConnection(MasterConStr))
                    {
                        log.Info("Initial Connection Attempts (Will fail as IP not yet added to firewall WhiteList)");
                        string DBConnectionAttempt = Helpers.TryToOpenSQLConnection(conn);
                        string MasterConnectionAttempt = Helpers.TryToOpenSQLConnection(MasterConn);
                        string WhiteListErrorMessageRaw = "";
                        if (MasterConnectionAttempt.Contains("Client with IP address") && MasterConnectionAttempt.Contains("is not allowed to access the server"))
                        {
                            WhiteListErrorMessageRaw = MasterConnectionAttempt;
                        }

                        if (DBConnectionAttempt.Contains("Client with IP address") && DBConnectionAttempt.Contains("is not allowed to access the server"))
                        {
                            WhiteListErrorMessageRaw = DBConnectionAttempt;
                        }

                        if ((DBConnectionAttempt == "Success") && (MasterConnectionAttempt == "Success"))
                        {
                            WhiteListErrorMessageRaw = DBConnectionAttempt;
                        }

                        if (WhiteListErrorMessageRaw == "")
                        {
                            throw new System.Exception("Non Firewall Error on SQL Connection: " + MasterConnectionAttempt + " !!!!!!!!!!!!!!!!EndMasterConnectionMessage!!!!!!!!!!!!!!!! " + DBConnectionAttempt + " !!!!!!!!!!!!!!!!EndDBConnectionMessage!!!!!!!!!!!!!!!!");
                        }
                        else
                        {
                            //Try to whitelist IP
                            AddToFirewallIP(WhiteListErrorMessageRaw, "AutomationFunctionIP");
                        }
                    }
                }

            }

            public void ResetAdminPW(string SQLAdminPassword)
            {
                try
                {
                    SQLServer.Update().WithAdministratorPassword(SQLAdminPassword).Apply();
                }
                catch (System.Exception PwResetError)
                {
                    throw new System.Exception("Reset of SQL Server Admin Password Failed: " + PwResetError.Message);
                }
            }
        }
        public static class Helpers
        {
            public static void ExecuteSQLStatement(string SQLStatement, System.Data.SqlClient.SqlConnection conn, TraceWriter log, string LogMessage, bool ThrowErrorOnFailure)
            {
                using (SqlCommand cmd = new SqlCommand(SQLStatement, conn))
                {
                    try
                    {
                        if (conn.State == System.Data.ConnectionState.Broken)
                        {
                            conn.Close();
                        }

                        if (conn.State == System.Data.ConnectionState.Closed)
                        {
                            conn.Open();
                        }


                        var rows = cmd.ExecuteNonQueryAsync().Result;
                        log.Info(LogMessage + "(Complete)");
                    }
                    catch (System.Exception e)
                    {
                        log.Error(LogMessage + "(Failed!!!)");
                        if(ThrowErrorOnFailure)
                        {
                            throw e;
                        }
                    }

                }
            }
            public static string TryToOpenSQLConnection(System.Data.SqlClient.SqlConnection conn)
            {
                try
                {
                    conn.Open();
                    return "Success";
                }
                catch (System.Data.SqlClient.SqlException sqlex)
                {
                    return sqlex.Errors[0].Message;
                }
                catch (System.Exception Nonsqlex)
                {
                    return Nonsqlex.Message;
                }
            }
            public static class GlobalConfigs
            {
                
                public static string Subscription = System.Environment.GetEnvironmentVariable("Subscription", EnvironmentVariableTarget.Process);
                public static string TenantId = System.Environment.GetEnvironmentVariable("TenantId", EnvironmentVariableTarget.Process);
                public static string MsiId = System.Environment.GetEnvironmentVariable("MsiId", EnvironmentVariableTarget.Process);
                public static string SQLServerID = System.Environment.GetEnvironmentVariable("SQLServerID", EnvironmentVariableTarget.Process);
                public static string ResourceGroup = System.Environment.GetEnvironmentVariable("ResourceGroup", EnvironmentVariableTarget.Process);
                public static string SQLDatabase = System.Environment.GetEnvironmentVariable("SQLDatabase", EnvironmentVariableTarget.Process);
                public static string OLAPReaderAccountName = System.Environment.GetEnvironmentVariable("OLAPReaderAccountName", EnvironmentVariableTarget.Process);
                public static bool UseMSIForSQL = System.Convert.ToBoolean(System.Environment.GetEnvironmentVariable("UseMSIForSQL", EnvironmentVariableTarget.Process));
                public static bool UseMSIForOLAP = System.Convert.ToBoolean(System.Environment.GetEnvironmentVariable("UseMSIForOLAP", EnvironmentVariableTarget.Process));
                public static string ApplicationId = System.Environment.GetEnvironmentVariable("ApplicationId", EnvironmentVariableTarget.Process);
                public static string AuthenticationKey = System.Environment.GetEnvironmentVariable("AuthenticationKey", EnvironmentVariableTarget.Process);
                public static string AASConnectionString = System.Environment.GetEnvironmentVariable("AASConnectionString", EnvironmentVariableTarget.Process);
            }
            public static void LogErrors(System.Exception e, TraceWriter log)
            {
                log.Error(e.Message.ToString());
                log.Error(e.StackTrace.ToString());
            }

            public static string GetAzureRestApiToken(string ServiceURI, bool UseMSI)
            {
                if (UseMSI == true)
                {
                    var tokenProvider = new AzureServiceTokenProvider();
                    return tokenProvider.GetAccessTokenAsync(ServiceURI).Result;
                }
                else
                {
                    //Service Principal https://management.azure.com/
                    var context = new AuthenticationContext("https://login.windows.net/" + Helpers.GlobalConfigs.TenantId);
                    ClientCredential cc = new ClientCredential(Helpers.GlobalConfigs.ApplicationId, Helpers.GlobalConfigs.AuthenticationKey);
                    AuthenticationResult result = context.AcquireTokenAsync(ServiceURI, cc).Result;
                    return result.AccessToken;
                }
            }
            public static AzureCredentials GetAzureCreds(bool UseMSI)
            {
                //MSI Login
                AzureCredentialsFactory f = new AzureCredentialsFactory();
                var msi = new MSILoginInformation(MSIResourceType.AppService);
                AzureCredentials creds;


                if (UseMSI == true)
                {
                    //MSI
                    creds = f.FromMSI(msi, AzureEnvironment.AzureGlobalCloud);
                }
                else
                {
                    //Service Principal
                    creds = f.FromServicePrincipal(Helpers.GlobalConfigs.ApplicationId, Helpers.GlobalConfigs.AuthenticationKey, Helpers.GlobalConfigs.TenantId, AzureEnvironment.AzureGlobalCloud);

                }

                return creds;
            }
        }


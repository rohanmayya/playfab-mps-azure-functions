using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.AuthenticationModels;
using PlayFab.ClientModels;
using PlayFab.MultiplayerModels;
using PlayFab.CloudScriptModels;

namespace Company.Function
{
    public static class GetMultiplayerServer
    {
        [FunctionName("GetMultiplayerServer")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");


            PlayFabSettings.staticSettings.TitleId = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID", EnvironmentVariableTarget.Process);
            PlayFabSettings.staticSettings.DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEVELOPER_SECRET_KEY", EnvironmentVariableTarget.Process);
            
            // Getting arguments from the caller (on the client-side)
            var context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());
            dynamic args = context.FunctionArgument;

            if (args != null && args["buildId"] != null && args["region"] != null)
            {
                string buildId = args["buildId"];
                string region = args["region"];


                var entityTokenRequest = new GetEntityTokenRequest();
                await PlayFabAuthenticationAPI.GetEntityTokenAsync(entityTokenRequest);
                
                // When I requested for a new server, I stored the session data in my database. "document" here is a data entry from in my database.
                // This can vary according to your games needs.
                if (document.ContainsKey("sessionId"))
                {
                    string sessionId = document["sessionId"];

                    var requestData = new GetMultiplayerServerDetailsRequest
                    {
                        BuildId = buildId,
                        SessionId = sessionId,
                        Region = region
                    };

                    try
                    {
                        var response = await PlayFabMultiplayerAPI.GetMultiplayerServerDetailsAsync(requestData);

                        return new OkObjectResult(response.Result);
                    }
                    catch (Exception e)
                    {
                        log.LogError(e.Message);
                    }
                }
                else
                {
                    var requestData = new RequestMultiplayerServerRequest
                    {
                        BuildId = buildId,
                        SessionId = Guid.NewGuid().ToString(),
                        PreferredRegions = new List<string> { "USWest" } // Enter your preferred regions here
                    };

                    log.LogInformation(requestData.ToString());

                    try
                    {
                        var response = await PlayFabMultiplayerAPI.RequestMultiplayerServerAsync(requestData);
                        
                        // Updating my database when a new session is created. This is dependent on your strategy.
                        if (!document.ContainsKey("sessionId")) document.Add("sessionId", response.Result.SessionId); 
                        await table.UpdateItemAsync(document);

                        return new OkObjectResult(response.Result);
                    }
                    catch (Exception e)
                    {
                        log.LogError(e.Message);
                    }
                }
            }
            return new OkObjectResult(null);
        }
    }


    public class FunctionExecutionContext<T>
    {
        public PlayFab.ProfilesModels.EntityProfileBody CallerEntityProfile { get; set; }
        public TitleAuthenticationContext TitleAuthenticationContext { get; set; }
        public bool? GeneratePlayStreamEvent { get; set; }
        public T FunctionArgument { get; set; }
    }

    public class TitleAuthenticationContext
    {
        public string Id { get; set; }
        public string EntityToken { get; set; }
    }
}

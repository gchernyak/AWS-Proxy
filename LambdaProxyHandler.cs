using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace AwsCoreProxy
{
    public class LambdaProxyHandler
    {
        public APIGatewayProxyResponse Handler(APIGatewayProxyRequest apigProxyEvent)
        {
            try
            {
                // getting aws api key value to map to internal api key
                var awsKey = apigProxyEvent.Headers["x-api-key"];
                var key = Environment.GetEnvironmentVariable(awsKey);
                // get internal api root Uri
                var apiRoot = Environment.GetEnvironmentVariable("InternalRoot");
                // get authentication header
                var auth = apigProxyEvent.Headers.Keys.Contains("Authorization");
                APIGatewayProxyResponse resposne = null;
                // make sure there is an auth header if not there is no point making an internal call
                if (!auth)
                {
                    resposne = new APIGatewayProxyResponse
                    {
                        Body = JsonConvert.SerializeObject("Unauthorized"),
                        Headers = apigProxyEvent.Headers,
                        StatusCode = 401,
                    };
                }
                else
                {
                    var authValue = apigProxyEvent.Headers["Authorization"];
                    // reconstructing call
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("apikey", key);
                        client.DefaultRequestHeaders.Add("Authorization", authValue);                     
                        var param = new StringBuilder("?");
                        string baseAction;
                        // checking if we have standard parameters
                        if (apigProxyEvent.QueryStringParameters != null)
                        {
                            foreach (var pair in apigProxyEvent.QueryStringParameters)
                            {
                                param.Append(pair.Key + "=" + pair.Value + "&");
                            }
                        }
                        baseAction = apigProxyEvent.PathParameters.First().Value;
                        string reconstructedUrl;
                        // if the user is using standard parameter notation ?param=000
                        if (param.Length > 0)
                        {
                            reconstructedUrl = apiRoot + baseAction.Substring(baseAction.IndexOf('/') + 1) +
                                                   param.ToString().TrimEnd('&');
                        }
                        // else we process as if there are no standard parameters /action/{id} - taking everything after the first segment
                        else
                        {
                            reconstructedUrl = apiRoot + baseAction.Substring(baseAction.IndexOf('/') + 1);
                        }
                        // we only allow GET
                        var result = client.GetAsync(reconstructedUrl);
                        // if we have a 200 we set the response
                        if ((int)result.Result.StatusCode == 200)
                        {
                            resposne = new APIGatewayProxyResponse
                            {
                                Body = result.Result.Content.ReadAsStringAsync().Result,
                                Headers = new Dictionary<string, string>()
                                {
                                    {"content-type", "application/json" }
                                },
                                StatusCode = (int)result.Result.StatusCode
                            };
                        }
                        // otherwise we send an error
                        else
                        {
                            resposne = new APIGatewayProxyResponse
                            {
                                Body = JsonConvert.SerializeObject("Something went wrong with your request."),
                                StatusCode = (int)result.Result.StatusCode
                            };
                        }
                    }
                }
                return resposne;
            }
            catch (Exception ex)
            {
                return new APIGatewayProxyResponse
                {
                    Body = JsonConvert.SerializeObject(ex.InnerException.Message),
                    Headers = apigProxyEvent.Headers,
                    StatusCode = 200,
                };
            }
        }
    }
}

using Autodesk.Forge;
using Autodesk.Forge.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RevitTranslator
{
    public class ForgeApi
    {
        public static string FORGE_SECRET = "";
        public static string FORGE_CLIENT_ID = "";

        public static async Task<string> GetViewerUrl(int port, string urn)
        {
            dynamic oauth = await Get2LeggedTokenAsync(new Scope[] { Scope.DataRead, Scope.ViewablesRead });
            return $"http://localhost:{port}?urn={urn}&token={oauth.access_token}&expiresin={oauth.expires_in}";
        }

        public static async Task<string> UploadRevitFile(string filepath)
        {
            string bucketKey = Guid.NewGuid().ToString();

            // authenticate with Forge
            dynamic oauth = await Get2LeggedTokenAsync(new Scope[] { Scope.BucketCreate, Scope.DataRead, Scope.DataCreate, Scope.DataWrite });

            // create the bucket
            BucketsApi buckets = new BucketsApi();
            buckets.Configuration.AccessToken = oauth.access_token;
            PostBucketsPayload bucketPayload = new PostBucketsPayload(bucketKey, null, PostBucketsPayload.PolicyKeyEnum.Transient);
            dynamic bucketResult = await buckets.CreateBucketAsync(bucketPayload);

            // upload the file/object, which will create a new object
            ObjectsApi objects = new ObjectsApi();
            objects.Configuration.AccessToken = oauth.access_token;
            dynamic uploadedObj;
            using (StreamReader streamReader = new StreamReader(filepath))
            {
                uploadedObj = await objects.UploadObjectAsync(bucketKey,
                        Path.GetFileName(filepath), (int)streamReader.BaseStream.Length, streamReader.BaseStream,
                        "application/octet-stream");
            }

            return Base64Encode(uploadedObj.objectId);
        }

        public static async Task<string[]> Translate(string urn, Action<int, string> onProgress)
        {
            // authenticate with Forge
            dynamic oauth = await Get2LeggedTokenAsync(new Scope[] { Scope.BucketCreate, Scope.DataRead, Scope.DataCreate, Scope.DataWrite });

            // start translating the file
            List<JobPayloadItem> outputs = new List<JobPayloadItem>()
                {
                    new JobPayloadItem(
                    JobPayloadItem.TypeEnum.Svf,
                    new List<JobPayloadItem.ViewsEnum>()
                    {
                        JobPayloadItem.ViewsEnum._2d,
                        JobPayloadItem.ViewsEnum._3d
                    })
                };
            JobPayload job = new JobPayload(new JobPayloadInput(urn), new JobPayloadOutput(outputs));
            DerivativesApi derivative = new DerivativesApi();
            derivative.Configuration.AccessToken = oauth.access_token;
            dynamic jobPosted = await derivative.TranslateAsync(job);

            var progress = await GetProgress(derivative, urn);
            onProgress(progress.Item1, progress.Item2);

            while (progress.Item1 < 100)
            {
                await Task.Delay(4000);
                progress = await GetProgress(derivative, urn);
                onProgress(progress.Item1, progress.Item2);
            }

            var lastManifest = progress.Item2;

            var derivativeUrnRegex = "\"urn\": \"([^\"]+)\"";

            var matches = Regex.Matches(lastManifest, derivativeUrnRegex);
            var listOfDerivativeUrns = new List<string>();
            foreach(Match match in matches)
            {
                listOfDerivativeUrns.Add(match.Groups[1].Value);
            }

            return listOfDerivativeUrns.ToArray();

        }

        public static async Task GetDerivativeManifest(string urn, string derivativeUrn, string outputFolder)
        {
            DerivativesApi derivative = new DerivativesApi();

            try
            {
                var data = await derivative.GetDerivativeManifestAsync(urn, derivativeUrn);

                var path = Path.Combine(outputFolder, Path.GetFileName(derivativeUrn));
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    data.WriteTo(fs);
                }
            }
            catch(Exception ex)
            {

            }
        }

        private static async Task<Tuple<int,string>> GetProgress(DerivativesApi derivative, string urn)
        {
            dynamic manifest = await derivative.GetManifestAsync(urn);
            var progress = (string.IsNullOrWhiteSpace(Regex.Match(manifest.progress, @"\d+").Value) ? 100 : Int32.Parse(Regex.Match(manifest.progress, @"\d+").Value));
            return new Tuple<int, string>(progress, PrettifyJson(JsonConvert.SerializeObject(manifest)));
        }

        private static async Task<dynamic> Get2LeggedTokenAsync(Scope[] scopes)
        {
            TwoLeggedApi apiInstance = new TwoLeggedApi();
            string grantType = "client_credentials";
            dynamic bearer = await apiInstance.AuthenticateAsync(
              FORGE_CLIENT_ID,
              FORGE_SECRET,
              grantType,
              scopes);
            return bearer;
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static async Task<string> GetMetadata(string urn)
        {
            DerivativesApi derivative = new DerivativesApi();

            dynamic metadata = await derivative.GetMetadataAsync(urn);
            var metadataJson = PrettifyJson(JsonConvert.SerializeObject(metadata));

            var guidRegex = "\"guid\": \"([^\"]+)\"";

            var matches = Regex.Matches(metadataJson, guidRegex);
            var listOfGuids = new List<string>();

            foreach (Match match in matches)
            {
                listOfGuids.Add(match.Groups[1].Value);
            }

            foreach(var guid in listOfGuids)
            {
                metadataJson += "\r\n\r\n" + JsonConvert.SerializeObject(await derivative.GetModelviewPropertiesAsync(urn, guid));
                metadataJson += "\r\n\r\n" + JsonConvert.SerializeObject(await derivative.GetModelviewMetadataAsync(urn, guid));
            }

            return metadataJson;

        }

        public static async Task<string> GetThumbnail(string urn, string outputFolder)
        {
            DerivativesApi derivative = new DerivativesApi();

            var thumbnail = await derivative.GetThumbnailAsync(urn);

            var thumbPath = Path.Combine(outputFolder, "thumbnail.png");
            using (var fs = new FileStream(thumbPath, FileMode.Create, FileAccess.Write))
            {
                thumbnail.WriteTo(fs);
            }

            return thumbPath;
        }

        private static string PrettifyJson(string json)
        {
            try
            {
                return JValue.Parse(json).ToString(Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }


        //private List<long> GetAllElements(dynamic objects)
        //{
        //    List<long> ids = new List<long>();
        //    foreach (KeyValuePair<string, dynamic> item in new DynamicDictionaryItems(objects))
        //    {
        //        foreach (KeyValuePair<string, dynamic> keys in item.Value.Dictionary)
        //        {
        //            if (keys.Key.Equals("objects"))
        //            {
        //                return GetAllElements(item.Value.objects);
        //            }
        //        }
        //        foreach (KeyValuePair<string, dynamic> element in objects.Dictionary)
        //        {
        //            if (!ids.Contains(element.Value.objectid))
        //                ids.Add(element.Value.objectid);
        //        }

        //    }
        //    return ids;
        //}

        //private Dictionary<string, object> GetProperties(long id, dynamic properties)
        //{
        //    Dictionary<string, object> returnProps = new Dictionary<string, object>();
        //    foreach (KeyValuePair<string, dynamic> objectProps in new DynamicDictionaryItems(properties.data.collection))
        //    {
        //        if (objectProps.Value.objectid != id) continue;
        //        string name = objectProps.Value.name;
        //        long elementId = long.Parse(Regex.Match(name, @"\d+").Value);
        //        returnProps.Add("ID", elementId);
        //        returnProps.Add("Name", name.Replace("[" + elementId.ToString() + "]", string.Empty));
        //        foreach (KeyValuePair<string, dynamic> objectPropsGroup in new DynamicDictionaryItems(objectProps.Value.properties))
        //        {
        //            if (objectPropsGroup.Key.StartsWith("__")) continue;
        //            foreach (KeyValuePair<string, dynamic> objectProp in new DynamicDictionaryItems(objectPropsGroup.Value))
        //            {
        //                if (!returnProps.ContainsKey(objectProp.Key))
        //                    returnProps.Add(objectProp.Key, objectProp.Value);
        //                else
        //                    Debug.Write(objectProp.Key);
        //            }
        //        }
        //    }
        //    return returnProps;
        //}



    }
}

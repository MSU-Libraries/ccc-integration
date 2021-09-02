
using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace CCCIntegration
{
    /// <summary>
    /// Exposes the CCC API
    /// NOTE: ALL of these functions throw their exceptions to the caller
    /// </summary>
    public class CCC
    {
        string access_token;
        string refresh_token;

        public static readonly IList<string> AllProducts = new System.Collections.ObjectModel.ReadOnlyCollection<string>
            (new List<String> {
                 "PRINT_COURSE_MATERIALS", "ELECTRONIC_COURSE_MATERIALS" });

        #region Helpers

        private static string GetConfig(string key)
        {
            return System.Configuration.ConfigurationManager.AppSettings[key];
        }

        private string BuildAccademicUrl(string method)
        {
            string endpoint = (GetConfig("ccc_accademic_endpoint") ?? "").TrimEnd('/') + @"/";
            return string.Format("{0}{1}", endpoint, method);
        }

        private string BuildTokenUrl(string method)
        {
            string endpoint = (GetConfig("ccc_token_endpoint") ?? "").TrimEnd('/') + @"/";
            return string.Format("{0}{1}", endpoint, method);
        }

        private JObject ResponseToJson(IRestResponse response)
        {
            JObject resp;

            // Try to parse the response into a json object
            try
            {
                resp = JObject.Parse(response.Content);
            }
            catch (Exception)
            {
                Exception ex = new Exception(
                    string.Format("CCC returned invalid JSON at {0}. Code: {1}. Response: {2}", 
                        response.ResponseUri, response.StatusCode, response.Content))
                {
                    Source = "CCC"
                };
                throw ex;
            }



            return resp;
        }

        private RestRequest AddCommonAttributes(IRestClient client, RestRequest request)
        {
            request.AddHeader("Authorization", String.Format("Bearer {0}", access_token));

            if (client.BaseUrl.ToString().Contains(GetConfig("ccc_token_endpoint")))
            {
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            }
            else
            {
                request.AddHeader("Content-Type", "application/json");
                request.RequestFormat = DataFormat.Json;
            }

            return request;
        }

        private IRestResponse CallApi(RestClient client, RestRequest request)
        {
            RefreshToken();
            request = AddCommonAttributes(client, request);
            IRestResponse response = client.Execute(request);
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                RefreshToken();
                response = client.Execute(request);
            }

            // Check for non-successful return codes
            if ((int)response.StatusCode >= 300)
            {
                string code = response.StatusCode.ToString();
                string description = response.Content;
                Exception ex = new Exception(
                    string.Format("CCC returned an error when calling {0}. Code: {1}. Description: {2}.", 
                        response.ResponseUri, code, description))
                {
                    Source = "CCC"
                };
                throw ex;
            }

            return response;
        }

        #endregion

        #region Authentication

        private void Authenticate()
        {
            if (string.IsNullOrWhiteSpace(access_token))
            {
                GetToken();
            }
            else
            {
                // try to call the API with the existing token to see if it expired
                var client = new RestClient(BuildAccademicUrl("users/locations"));
                var request = new RestRequest(Method.GET);
                CallApi(client, request);
            }
        }

        private void GetToken()
        {
            var client = new RestClient(BuildTokenUrl("accessToken"));
            var request = new RestRequest(Method.POST);
            request.AddParameter("username", GetConfig("ccc_username"));
            request.AddParameter("password", GetConfig("ccc_password"));
            request.AddParameter("client_id", GetConfig("ccc_client_id"));
            request.AddParameter("client_secret", GetConfig("ccc_client_secret"));
            request.AddParameter("grant_type", "password");
            IRestResponse response = client.Execute(request);
            JObject resp = ResponseToJson(response);

            // Prase the response
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                access_token = (string)resp["access_token"];
                refresh_token = (string)resp["refresh_token"];
            }
            else
            {
                string code = (string)resp["status"] ?? response.StatusCode.ToString();
                string description = (string)resp["error"] ?? response.Content;
                Exception ex = new Exception(
                    string.Format("CCC returned an error when calling {0}. Code: {1}. Description: {2}.", 
                        response.ResponseUri, code, description))
                {
                    Source = "CCC"
                };
                throw ex;
            }
        }

        private void RefreshToken()
        {
            if (string.IsNullOrWhiteSpace(refresh_token)) GetToken();
            else
            {
                var client = new RestClient(BuildTokenUrl("accessToken"));
                var request = new RestRequest(Method.POST);
                request.AddParameter("client_id", GetConfig("ccc_client_id"));
                request.AddParameter("client_secret", GetConfig("ccc_client_secret"));
                request.AddParameter("grant_type", "refresh_token");
                request.AddParameter("refresh_token", refresh_token);
                IRestResponse response = client.Execute(request);
                JObject resp = ResponseToJson(response);

                // Prase the response
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    access_token = (string)resp["access_token"];
                }
                else
                {
                    // else, get a new token
                    GetToken();
                }
            }
        }
        #endregion

        #region Locations

        public JObject GetLocations()
        {
            var client = new RestClient(BuildAccademicUrl("users/locations"));
            var request = new RestRequest(Method.GET);
            var response = CallApi(client, request);
            return ResponseToJson(response);
        }
        #endregion

        #region Courses

        public JObject CreateCourse(int no_of_recipients, string course_name, string instructor, string course_number, 
            string course_reference, DateTime start_of_term, string netid, string name = "", string external_course_id = "",
            string user_name = "", string location_id = "")
        {
            var client = new RestClient(BuildAccademicUrl("courses/"));
            var request = new RestRequest(Method.POST);

            if (string.IsNullOrEmpty(location_id))
            {
                // If no location is provided we'll grab the first one available
                // since most uses have only one location
                JObject locations = GetLocations();
                location_id = locations["locations"].First["id"].ToString();
            }

            JObject body = new JObject
            {
                { "name", name },
                { "user_name", user_name },
                { "location_id", location_id },
                { "external_course_id", external_course_id },
                { "no_of_recipients", no_of_recipients },
                { "university_inst", GetConfig("university_inst") },
                { "start_of_term", start_of_term.ToString("yyyy-MM-dd") },
                { "course_name", course_name },
                { "instructor", instructor },
                { "course_number", course_number },
                { "course_reference", course_reference },
                { "accounting_reference", GetConfig("CccAcctRefNum") },
                { "order_entered_by", netid }
            };

            request.AddParameter("application/json", body, ParameterType.RequestBody);

            var response = CallApi(client, request);
            return ResponseToJson(response);
        }

        public JObject GetCourse(string course_id)
        {
            var client = new RestClient(BuildAccademicUrl(string.Format("courses/{0}", course_id)));
            var request = new RestRequest(Method.GET);
            var response = CallApi(client, request);
            return ResponseToJson(response);
        }

        public JObject GetCourses(string filter = "", int page_number = 1, int page_size = 100)
        {
            var client = new RestClient(BuildAccademicUrl("courses"));
            var request = new RestRequest(Method.GET);
            request.AddQueryParameter("filter", filter);
            request.AddQueryParameter("page_number", page_number.ToString());
            request.AddQueryParameter("page_size", page_size.ToString());

            var response = CallApi(client, request);
            return ResponseToJson(response);
        }

        public void UpdateCourse(string course_id, int no_of_recipients, string course_name, string instructor, string course_number,
            string course_reference, DateTime start_of_term, string netid, string name = "", string external_course_id = "",
            string user_name = "", string location_id = "")
        {
            var client = new RestClient(BuildAccademicUrl(string.Format("courses/{0}", course_id)));
            var request = new RestRequest(Method.PUT);

            if (string.IsNullOrEmpty(location_id))
            {
                // If no location is provided we'll grab the first one available
                // since most uses have only one location
                JObject locations = GetLocations();
                location_id = locations["locations"].First["id"].ToString();
            }

            JObject body = new JObject
            {
                { "name", name },
                { "user_name", user_name },
                { "location_id", location_id },
                { "external_course_id", external_course_id },
                { "no_of_recipients", no_of_recipients },
                { "university_inst", GetConfig("university_inst") },
                { "start_of_term", start_of_term.ToString("yyyy-MM-dd") },
                { "course_name", course_name },
                { "instructor", instructor },
                { "course_number", course_number },
                { "course_reference", course_reference },
                { "accounting_reference", GetConfig("CccAcctRefNum") },
                { "order_entered_by", netid }
            };

            request.AddParameter("application/json", body, ParameterType.RequestBody);

            CallApi(client, request);

        }

        public void DeleteCourse(string course_id)
        {
            IRestResponse resp = null;
            var client = new RestClient(BuildAccademicUrl(string.Format("courses/{0}", course_id)));
            var request = new RestRequest(Method.DELETE);

            try
            {
                resp = CallApi(client, request);
            }
            catch (Exception ex)
            {
                // ignore 404 since that means it was already deleted
                if (resp.StatusCode != HttpStatusCode.NotFound) throw ex;
            }
        }

        #endregion

        #region Course Items

        public JObject CreateCourseItem(string course_id, string publication_id, string product, string pub_year,
            int requested_pages, string page_ranges, string article_chapter, string reference, string author_editor,
            string portion = "PAGE")
        {
            var client = new RestClient(BuildAccademicUrl(string.Format("courses/{0}/items", course_id)));
            var request = new RestRequest(Method.POST);

            // determine format from product
            List<string> format = new List<string>();
            if (product == "PRINT_COURSE_MATERIALS") format = new List<string>() { "PHOTOCOPY_COURSEPACK" };
            else format = new List<string>() { "ELECTRONIC_COURSEPACK" };

            JObject body = new JObject
            {
                { "publication_id", publication_id },
                { "product", product },
                { "portion", portion },
                { "pub_year", pub_year },
                { "format", JArray.FromObject(format) },
                { "article_chapter", article_chapter },
                { "author_editor", string.IsNullOrEmpty(author_editor) ? "TBD" : author_editor },
                { "reference", reference }
            };
            if (portion == "PAGE") body.Add("requested_pages", requested_pages);
            if (portion == "PAGE") body.Add("page_ranges", page_ranges);

            request.AddParameter("application/json", body, ParameterType.RequestBody);

            var response = CallApi(client, request);
            JObject createResp = ResponseToJson(response);

            // This is needed because CCC throws an error saying it in an invalid state (pricing status)
            System.Threading.Thread.Sleep(1000);

            string course_item_id = createResp["id"].ToString();

            // Now go back and set the author_editor
            try
            {
                SyncFields(course_id, course_item_id);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("INVALID_RESOURCE_STATE"))
                {
                    // wait a little and try again
                    System.Threading.Thread.Sleep(2000);
                    try
                    {
                        SyncFields(course_id, course_item_id);
                    }
                    catch (Exception ex2)
                    {
                        if (ex2.Message.Contains("INVALID_RESOURCE_STATE"))
                        {
                            // wait a little longer and try again
                            System.Threading.Thread.Sleep(2000);
                            SyncFields(course_id, course_item_id);
                        }
                        else throw ex2;
                    }
                }
                else throw ex;
            }


            // Send back the latest course item info
            return GetCourseItem(course_id, course_item_id);
        }

        public void SyncFields(string course_id, string course_item_id)
        {
            JObject getResp = GetCourseItem(course_id, course_item_id);

            // Get the new author
            string author_editor = getResp["work_metadata"]["author"].ToString();

            UpdateCourseItem(course_id, course_item_id, getResp["requested_pages"] == null ? 0 : (int)getResp["requested_pages"],
                getResp["page_ranges"] == null ? "" : (string)getResp["page_ranges"],
                author_editor, (string)getResp["article_chapter"], (string)getResp["reference"],
                getResp["format"].ToObject<List<string>>());
        }

        public JObject GetCourseItem(string course_id, string course_item_id)
        {
            var client = new RestClient(BuildAccademicUrl(string.Format("courses/{0}/items/{1}", course_id, course_item_id)));
            var request = new RestRequest(Method.GET);
            var response = CallApi(client, request);
            return ResponseToJson(response);
        }

        public JObject GetCourseItems(string course_id, string filter = "", int page_number = 1, int page_size = 100)
        {
            var client = new RestClient(BuildAccademicUrl(string.Format("courses/{0}/items", course_id)));
            var request = new RestRequest(Method.GET);
            request.AddQueryParameter("filter", filter);
            request.AddQueryParameter("page_number", page_number.ToString());
            request.AddQueryParameter("page_size", page_size.ToString());

            var response = CallApi(client, request);
            return ResponseToJson(response);
        }


        public void UpdateCourseItem(string course_id, string course_item_id, int requested_pages,
            string page_ranges, string author_editor, string article_chapter, string reference, List<string> format,
            string portion = "PAGE")
        {
            var client = new RestClient(BuildAccademicUrl(string.Format("courses/{0}/items/{1}", course_id, course_item_id)));
            var request = new RestRequest(Method.PUT);

            JObject body = new JObject
            {
                { "portion", portion },
                { "format", JArray.FromObject(format) },
                { "article_chapter", article_chapter },
                { "author_editor", author_editor },
                { "reference", reference }
            };
            if (portion == "PAGE") body.Add("requested_pages", requested_pages);
            if (portion == "PAGE") body.Add("page_ranges", page_ranges);

            request.AddParameter("application/json", body, ParameterType.RequestBody);

            CallApi(client, request);
        }

        public void DeleteCourseItem(string course_id, string course_item_id)
        {
            IRestResponse resp = null;
            var client = new RestClient(BuildAccademicUrl(string.Format("courses/{0}/items/{1}", course_id, course_item_id)));
            var request = new RestRequest(Method.DELETE);
            try
            {
                resp = CallApi(client, request);
            }
            catch (Exception ex)
            {
                // ignore 404 since that means it was already deleted
                if (resp.StatusCode != HttpStatusCode.NotFound) throw ex;
            }
        }

        #endregion

        #region Works
        public JObject SearchWorks(string issn, string isbn, string title, string pub_year, int page_number = 1, int page_size = 100)
        {
            var client = new RestClient(BuildAccademicUrl("works/search"));
            var request = new RestRequest(Method.POST);
            request.AddQueryParameter("page_number", page_number.ToString());
            request.AddQueryParameter("page_size", page_size.ToString());

            JObject body = new JObject
            {
                { "issn", issn },
                { "isbn", isbn },
                { "title", title },
                { "pub_year", pub_year }
            };
            request.AddParameter("application/json", body, ParameterType.RequestBody);

            var response = CallApi(client, request);
            return ResponseToJson(response);
        }

        public JObject GetPrice(string publication_id, List<string> products = null, string portion = "PAGE", string pub_year = "", 
            int no_of_recipients = 1, int requested_pages = 1) 
        {
            var client = new RestClient(BuildAccademicUrl("estimates"));
            var request = new RestRequest(Method.POST);

            JObject body = new JObject
            {
                { "publication_id", publication_id },
                { "products", JToken.FromObject(products ?? AllProducts) },
                { "portion", portion },
                { "pub_year", pub_year },
                { "no_of_recipients", no_of_recipients }
            };
            if (portion == "PAGE") body.Add("requested_pages", requested_pages);
            request.AddParameter("application/json", body, ParameterType.RequestBody);

            var response = CallApi(client, request);
            return ResponseToJson(response);
        }

        #endregion
    }
}

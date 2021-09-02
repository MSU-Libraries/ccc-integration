using System;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using Newtonsoft.Json.Linq;

namespace CCCIntegration
{
    public partial class SearchCCCIntegration : BasePage
    {
        private int CurrentPageCount { get; set; }

        #region Page Load
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                errorPlaceholder.InnerHtml = "";

                // Do only the first time the page is loaded
                if (!IsPostBack)
                {
                    // Hide the pager & estimation fields on first load
                    ulPagger.Visible = false;
                    estFields.Visible = false;
                    noResultsFound.Visible = false;
                }
            }
            catch (Exception ex)
            {
                HandleError("Page load", ex);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Processes the exception
        /// </summary>
        /// <param name="location">location where the error occured</param>
        /// <param name="ex">Exception object</param>
        private void HandleError(string location, Exception ex)
        {
            errorPlaceholder.InnerHtml = "<p class='error'>An error occured while performing action: " + location;
            errorPlaceholder.InnerHtml += "<br/>" + ex.Message + "</br>" + ex.StackTrace;
            errorPlaceholder.InnerHtml += "</p>";
        }

        #endregion

        #region Processors (Do not handle Exceptions raised!)

        /// <summary>
        /// Search the API given the form parameters
        /// </summary>
        /// <param name="pageNumber">Page number of the results, defaults to page 1</param>
        protected void SearchWorks(int pageNumber = 1)
        {
            string issn = "", isbn = "", title = "";

            switch (termType.SelectedItem.Value)
            {
                case "issn":
                    issn = term.Text;
                    break;
                case "isbn":
                    isbn = term.Text;
                    break;
                case "title":
                    title = term.Text;
                    break;
            }

            JObject results = API.SearchWorks(issn, isbn, title, pubYear.Text, pageNumber, resultsGrid.PageSize);

            if ((int)results["total_count"] > 0)
            {
                resultsGrid.DataSource = AddDataToResults((JArray)results["works"]); ;
                CurrentPageCount = (int)Math.Ceiling((double)((int)results["total_count"] / resultsGrid.PageSize));
                resultsGrid.DataBind();
                noResultsFound.Visible = false;
                resultsGrid.Visible = true;


                // only bother with pagination if there is something to paginate
                if (CurrentPageCount > 1)
                {
                    rptPages.DataSource = GetPaginationList(CurrentPageCount, pageNumber);
                    rptPages.DataBind();
                    ulPagger.Visible = true;
                }
                else
                {
                    ulPagger.Visible = false;
                }

                // display the form fields used in estimation of publication items
                estFields.Visible = true;
            }
            else
            {
                resultsGrid.Visible = false;
                estFields.Visible = false;
                ulPagger.Visible = false;
                noResultsFound.Visible = true;
            }
        }

        /// <summary>
        /// Add additional information to the results the come back from the API,
        /// this saves us some logic in the templates
        /// </summary>
        /// <returns>The same results with the additional fields</returns>
        /// <param name="results">Results to update from the API</param>
        protected JArray AddDataToResults(JArray results)
        {
            foreach (JObject result in results)
            {
                // Check if there is a date we could use
                string pub_year = pubYear.Text;
                if (!string.IsNullOrEmpty((string)result["date"]) && result["date"].ToString().Length >= 4)
                {
                    string tmp_year = result["date"].ToString().Substring(0,4);
                    if (int.TryParse(tmp_year, out int tmp_year_int))
                    {
                        if (tmp_year_int > 1700 && tmp_year_int < 3000)
                        {
                            pub_year = tmp_year;
                        }
                    }
                }
                result.Add(new JProperty("pub_year", pub_year));

                if (result["isbn"] != null)
                {
                    result.Add(new JProperty("ident_type", "ISBN"));
                    result.Add(new JProperty("ident_value", result["isbn"]));
                }
                else if (result["issn"] != null)
                {
                    result.Add(new JProperty("ident_type", "ISSN"));
                    result.Add(new JProperty("ident_value", result["issn"]));
                }
                else
                {
                    result.Add(new JProperty("ident_type", "- "));
                    result.Add(new JProperty("ident_value", "-"));
                }

                string print_perm = "Unknown", print_perm_icon = "help-circle", print_perm_color="yellow";
                string electronic_perm = "Unknown", electronic_perm_icon = "help-circle", electronic_perm_color = "yellow";
                foreach (JObject right in (JArray)result["rights"])
                {
                    if (right["product"].ToString() == "PRINT_COURSE_MATERIALS")
                    {
                        print_perm = "Status: <b>" + right["resolution"] + "</b>";
                        switch (right["resolution"].ToString())
                        {
                            case "GRANT":
                                print_perm_icon = "check-circle";
                                print_perm_color = "green";
                                break;
                            case "DENY":
                                print_perm_icon = "x-circle";
                                print_perm_color = "red";
                                break;
                            case "SPR":
                                print_perm_icon = "alert-circle";
                                print_perm_color = "orange";
                                break;
                            default:
                                print_perm_icon = "help-circle";
                                print_perm_color = "yellow";
                                break;
                        }
                    }
                    else if (right["product"].ToString() == "ELECTRONIC_COURSE_MATERIALS")
                    {
                        electronic_perm = "Status: <b>" + right["resolution"] + "</b>";
                        switch (right["resolution"].ToString())
                        {
                            case "GRANT":
                                electronic_perm_icon = "check-circle";
                                electronic_perm_color = "green";
                                break;
                            case "DENY":
                                electronic_perm_icon = "x-circle";
                                electronic_perm_color = "red";
                                break;
                            case "SPR":
                                electronic_perm_icon = "alert-circle";
                                electronic_perm_color = "orange";
                                break;
                            default:
                                electronic_perm_icon = "help-circle";
                                electronic_perm_color = "yellow";
                                break;
                        }
                    }
                }

                result.Add(new JProperty("print_permission", print_perm));
                result.Add(new JProperty("print_permission_icon", print_perm_icon));
                result.Add(new JProperty("print_permission_icon_color", print_perm_color));
                result.Add(new JProperty("electronic_permission", electronic_perm));
                result.Add(new JProperty("electronic_permission_icon", electronic_perm_icon));
                result.Add(new JProperty("electronic_permission_icon_color", electronic_perm_color));
            }
            return results;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Pagination repeater. Adds the required attributes and classes to each item
        /// in the pager.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void RptPages_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            try
            {
                if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem)
                {
                    return;
                }

                string pageValue = (string)e.Item.DataItem;
                int setPageNumber = resultsGrid.PageIndex + 1;

                LinkButton lnkPageNum = (LinkButton)e.Item.FindControl("lnkPageNumber");

                // Disable the elipsis items
                if (pageValue == "...")
                {
                    HtmlGenericControl liPageNum = (HtmlGenericControl)e.Item.FindControl("lnkPageNumber").Parent;
                    liPageNum.Attributes.Add("class", "page-item disabled");
                    lnkPageNum.Text = pageValue;
                    return;
                }
                int pageNumber = Convert.ToInt32(pageValue);

                // Update the text and command value for the page
                // The command value is used by the event handler to determine which page to display
                lnkPageNum.Text = pageValue;
                lnkPageNum.CommandArgument = pageValue;
                lnkLast.CommandArgument = CurrentPageCount.ToString();

                // Disable the current page index
                if (pageNumber == setPageNumber)
                {
                    HtmlGenericControl liPageNum = (HtmlGenericControl)e.Item.FindControl("lnkPageNumber").Parent;
                    liPageNum.Attributes.Add("class", "page-item active");
                }

                // Disable previous/next buttons if first or last page
                if (setPageNumber == 1)
                {
                    ((HtmlGenericControl)e.Item.FindControl("lnkPrev").Parent).Attributes.Add("class", "page-item disabled");
                    ((HtmlGenericControl)e.Item.FindControl("lnkFirst").Parent).Attributes.Add("class", "page-item disabled");
                }
                else // re-enable them otherwise
                {
                    ((HtmlGenericControl)e.Item.FindControl("lnkPrev").Parent).Attributes.Add("class", "page-item");
                    ((HtmlGenericControl)e.Item.FindControl("lnkFirst").Parent).Attributes.Add("class", "page-item");
                }
                if (setPageNumber == CurrentPageCount)
                {
                    ((HtmlGenericControl)e.Item.FindControl("lnkNext").Parent).Attributes.Add("class", "page-item disabled");
                    ((HtmlGenericControl)e.Item.FindControl("lnkLast").Parent).Attributes.Add("class", "page-item disabled");
                }
                else // re-enable them otherwise
                {
                    ((HtmlGenericControl)e.Item.FindControl("lnkNext").Parent).Attributes.Add("class", "page-item");
                    ((HtmlGenericControl)e.Item.FindControl("lnkLast").Parent).Attributes.Add("class", "page-item");
                }
            }
            catch (Exception ex)
            {
                HandleError("Loading the search results on the page", ex);
            }
        }

        protected void BtnSearch_Click(object sender, EventArgs e)
        {
            try
            {
                if (Page.IsValid)
                {
                    resultsGrid.PageIndex = 0;
                    SearchWorks(1);
                }
            } 
            catch (Exception ex)
            {
                HandleError("Searching", ex);
            }
        }

        protected void LnkFirst_Click(object sender, EventArgs e)
        {
            try
            {
                resultsGrid.PageIndex = 0;
                SearchWorks(1);
            }
            catch (Exception ex)
            {
                HandleError("Navigating to the first search results page", ex);
            }
        }

        protected void LnkPageNumber_Click(object sender, EventArgs e)
        {
            try
            {
                LinkButton btn = (LinkButton)sender;
                resultsGrid.PageIndex = Convert.ToInt32(btn.CommandArgument) - 1;
                SearchWorks(resultsGrid.PageIndex + 1);
            }
            catch (Exception ex)
            {
                HandleError("Updating search results page", ex);
            }
        }

        protected void LnkNext_Click(object sender, EventArgs e)
        {
            try
            {
                resultsGrid.PageIndex = resultsGrid.PageIndex + 1;
                SearchWorks(resultsGrid.PageIndex + 1);
            }
            catch (Exception ex)
            {
                HandleError("Navigating to the next search results page", ex);
            }
        }

        protected void LnkPrev_Click(object sender, EventArgs e)
        {
            try
            {
                resultsGrid.PageIndex = resultsGrid.PageIndex - 1;
                SearchWorks(resultsGrid.PageIndex + 1);
            }
            catch (Exception ex)
            {
                HandleError("Navigating to the previous search results page", ex);
            }
        }

        protected void LnkLast_Click(object sender, EventArgs e)
        {
            try
            {
                LinkButton btn = (LinkButton)sender;
                resultsGrid.PageIndex = Convert.ToInt32(btn.CommandArgument) - 1;
                SearchWorks(resultsGrid.PageIndex + 1);
            }
            catch (Exception ex)
            {
                HandleError("Navigating to the last search results page", ex);
            }
        }

        #endregion

        #region AJAX methods

        /// <summary>
        /// Get the price estimate for the publication
        /// </summary>
        /// <returns>The price data from CCC</returns>
        /// <param name="publication_id">Publication identifier in CCC</param>
        /// <param name="num_copies">Number copies</param>
        /// <param name="num_pages">Number pages</param>
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetPrice(string publication_id, string num_copies, string num_pages, bool entire_book)
        {
            CCC api = new CCC();
            int int_num_pages = 0;
            int int_num_copies = 0;
            string portion = "PAGE";

            try
            {
                int_num_copies = Convert.ToInt32(num_copies);
                int_num_pages = Convert.ToInt32(num_pages);
                if (entire_book) portion = "ENTIRE_BOOK";
            }
            catch
            {
                return "{}";
            }

            JObject price = API.GetPrice(publication_id, no_of_recipients: int_num_copies, requested_pages: int_num_pages, portion: portion);
            return ((JArray)price["estimates"]).ToString();
        }

        #endregion

    }
}

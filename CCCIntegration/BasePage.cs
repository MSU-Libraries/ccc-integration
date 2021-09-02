using System.Collections.Generic;
using System.Web;
using System.Web.UI;

namespace CCCIntegration
{
    public class BasePage: Page
    {
        public object SafeEval(object container, string expression, string default_value = "")
        {
            try
            {
                return DataBinder.Eval(container, expression);
            }
            catch (HttpException e)
            {
                // Write error details to minimize the harm caused by suppressed exception 
                Trace.Write("DataBinding", "Failed to process the Eval expression", e);
            }

            return default_value;
        }
        public object SafeEvalNoQuote(object container, string expression, string default_value = "")
        {
            try
            {
                return DataBinder.Eval(container, expression).ToString().Replace("\"", "");
            }
            catch (HttpException e)
            {
                // Write error details to minimize the harm caused by suppressed exception 
                Trace.Write("DataBinding", "Failed to process the Eval expression", e);
            }

            return default_value;
        }

        private static CCC _api { get; set; }
        public static CCC API { get
            { 
                if (_api == null) _api = new CCC();
                return _api;
            }
        }

        public static List<string> GetPaginationList(int currentPageCount = 1, int pageNumber = 1)
        {
            List<string> pagination = new List<string>();

            int displayRange = 3; // number of page numbers to display on either side of the current page number
            // Handle if we need to add elipsis to one or both sides of the pager
            if (pageNumber - displayRange > 1 || pageNumber + displayRange < currentPageCount)
            {
                // Check if the left side needs an elipsis
                if (pageNumber - displayRange > 1)
                {
                    pagination.Add("...");
                    for (int i = pageNumber - displayRange; i <= pageNumber; i++)
                    {
                        pagination.Add(i.ToString());
                    }
                }
                else
                {
                    for (int i = 1; i <= pageNumber; i++)
                    {
                        pagination.Add(i.ToString());
                    }
                }
                // Check if the right side needs an elipsis
                if (pageNumber + displayRange < currentPageCount)
                {
                    for (int i = pageNumber + 1; i <= pageNumber + displayRange; i++)
                    {
                        pagination.Add(i.ToString());
                    }
                    pagination.Add("...");
                }
                else
                {
                    for (int i = pageNumber + 1; i <= currentPageCount; i++)
                    {
                        pagination.Add(i.ToString());
                    }
                }
            }
            else // otherwise, we just add all the pages as-is!
            {
                for (int i = 1; i <= currentPageCount; i++)
                {
                    pagination.Add(i.ToString());
                }
            }
            return pagination;
        }

    }
}
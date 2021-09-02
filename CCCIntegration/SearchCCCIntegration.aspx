<%@ Page Title="Search CCC" Language="C#" MasterPageFile="~/Site.Master" CodeBehind="SearchCCCIntegration.aspx.cs" Inherits="CCCIntegration.SearchCCCIntegration" AutoEventWireup="true" MaintainScrollPositionOnPostback="true" %>

<asp:Content runat="server" ID="BodyContent" ContentPlaceHolderID="MainContent">
    <script>
        function parsePrice(data, perm_item) {
            estimates = jQuery.parseJSON(data["d"]);
            print_content = "";
            electronic_content = "";
            
            estimates.forEach(function (item, index) {
                // build tooltip info
                label = "Print Permissions";
                if (item["product"] == "ELECTRONIC_COURSE_MATERIALS"){
                    label = "Electronic Permissions";
                }
                tooltip = "Status: <b>" + item["resolution"].replace(/'/g, " &#39;") + "</b></br>Terms:";
                tooltip += (item["terms"].length == 0) ? " -</br>" : "<br/>";
                item["terms"].forEach(function(t_item, t_index){
                    tooltip += "(Type: " + t_item["type"].replace(/'/g, " &#39;") + ") " + t_item["text"].replace(/'/g, " &#39;") + "</br>";
                });
                if ("decision_reason" in item && item["decision_reason"].length > 0){
                    tooltip += "</br>Decision Reason: " + item["decision_reason"].replace(/'/g, " &#39;");
                }
        
                // build element
                node = "<span data-toggle='popover' data-html='true'  data-trigger='hover'"
                node += "title='" + label + "' data-content='" + tooltip + "'>"
                if ("price" in item){
                    
                    if(item["resolution"] == "GRANT"){
                        node += "<span class='text-success align-text-bottom'>";
                    }
                    else if(item["resolution"] == "DENY") {
                        node += "<span class='text-danger align-text-bottom'>";
                    }
                    else if(item["resolution"] == "SPR") {
                        node += "<span style='color:orange;' class='align-text-bottom'>";
                    }
                    node += "$" + item["price"].toFixed(2);
                    node += "</span>";
                }
                else {
                    node += "<i data-feather='x-circle' style='color:red;' class='align-text-bottom'></i>";
                }
                node += "</span>";
        
                // set element to print or electronic
                if (item["product"] == "PRINT_COURSE_MATERIALS") {
                    print_content = node;
                }
                else if (item["product"] == "ELECTRONIC_COURSE_MATERIALS") {      
                    electronic_content = node;   
                }
             });
        
            perm_item.innerHTML = print_content + " | " + electronic_content;
            $('[data-toggle="popover"]').popover();
            feather.replace();
        }
        
        function getPrice(publication_id, element){
            var num_copies = $("#num_copies")[0].value;
            var num_pages = $("#num_pages")[0].value;
            var entire_book = $("#chk_entire_book")[0].checked;
            var perm_item = element.offsetParent.previousSibling;
        
            $.ajax({
                url: "SearchCCCIntegration.aspx/GetPrice",
                data: "{ 'publication_id':'" + publication_id + "', 'num_copies':'" + num_copies + "','num_pages':'" + num_pages + "', 'entire_book':" + entire_book + "}",
                dataType: "json",
                type: "POST",
                beforeSend: function() { 
                    element.innerHTML = "Loading..."; 
                },
                complete: function() { 
                   element.innerHTML = "Get Price"; 
                },
                contentType: "application/json; charset=utf-8",
                success:function(data) {
                    parsePrice(data, perm_item);
                },
                error: function (XMLHttpRequest, textStatus, errorThrown) {
                   alert("Error getting price. " + textStatus + ". Error: " + errorThrown);
                }
            });
        }
    </script>
    
    <span runat ="server" id="errorPlaceholder" />
    
    <!-- Search form -->
    <div class="form"> 
        <div class="row w-100">
         <div class="form-group col-sm">
            <asp:RadioButtonList ID="termType" runat="server" class="form-check form-check-inline" RepeatLayout="Flow" Style="height:1rem;">
                <asp:ListItem Text="ISBN" Value="isbn" Selected="true" class="form-check-input pr-2" />
                <asp:ListItem Text="ISSN" Value="issn" class="form-check-input pr-2" />
                <asp:ListItem Text="Title" Value="title" class="form-check-input" />
            </asp:RadioButtonList>   
                
            <asp:TextBox ID="term" runat="server" CssClass="form-control mt-2" aria-label="Enter an ISSN, ISBN, or Title" placeholder="Ex: 978-3-16-148410-0"></asp:TextBox>
            <asp:RequiredFieldValidator ID="termValidator" runat="server"  EnableClientScript="true"
                CssClass="text-danger" ErrorMessage="Enter a term" ControlToValidate="term">
                </asp:RequiredFieldValidator> 
            </div>
            
            <div class="form-group col-sm">
                <asp:Label ID="pubYearLbl" AssociatedControlId="pubYear" Text="Publication Year" CssClass="pr-2"runat="server" />
                <asp:TextBox ID="pubYear" runat="server" CssClass="form-control" placeholder="Ex: 2005"></asp:TextBox>
                <asp:RequiredFieldValidator ID="yearValidator" runat="server" EnableClientScript="true" CssClass="text-danger" 
                    ErrorMessage="Enter a publication year" ControlToValidate="pubYear">
                    </asp:RequiredFieldValidator> 
                <asp:RegularExpressionValidator ID="yearValueValidator" runat="server" CssClass="text-danger" Style="margin-left:-10.5rem;" EnableClientScript="true" 
                    ErrorMessage="Publication year must be a 4-digit number (ex: 2021)" ControlToValidate="pubYear"   
                    ValidationExpression="\d{4}"></asp:RegularExpressionValidator> 
            </div>
        </div>
        <div class="row w-100 pl-3">
            <asp:Button ID="btnSearch" runat="server" Text="Search" class="btn btn-light btn-pmhome" OnClick="BtnSearch_Click" />
        </div>
    </div>
    <hr>

    <!-- Results -->
    <div>
        <span id="noResultsFound" runat="server">No results found.</span>
        <asp:GridView ID="resultsGrid" runat="server" AutoGenerateColumns="false" 
           CssClass="table table-hover table-sm border-0" 
           GridLines="None">
        <HeaderStyle CssClass="thead-light" />
        <Columns>
            <asp:TemplateField HeaderText="Title">
                <ItemTemplate><%# SafeEval(Container.DataItem, "title") %></ItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="Pub Year">
                <ItemTemplate><%# SafeEval(Container.DataItem, "pub_year") %></ItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="Publisher">
                <ItemTemplate><%# SafeEval(Container.DataItem, "publisher") %></ItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="Identifier">
                <ItemTemplate><%# SafeEval(Container.DataItem, "ident_type") %>: <%# SafeEval(Container.DataItem, "ident_value") %></ItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="Permissions">
                <HeaderStyle CssClass="text-center" />
                <ItemStyle HorizontalAlign="Center"/>
                <ItemTemplate id='perms'>
                  <span data-toggle="popover" data-html="true"  data-trigger="hover" title="Print Permissions" 
                        data-content='<%# SafeEval(Container.DataItem, "print_permission", "yellow") %>'>
                      <i data-feather='<%# SafeEval(Container.DataItem, "print_permission_icon", "help-circle") %>' class="align-text-bottom"
                         style='color:<%# SafeEval(Container.DataItem, "print_permission_icon_color", "yellow") %>;'></i>
                  </span>
                  |
                  <span data-toggle="popover" data-html="true"  data-trigger="hover" title="Electronic Permissions" 
                        data-content='<%# SafeEval(Container.DataItem, "electronic_permission", "yellow") %>'>
                  <i data-feather='<%# SafeEval(Container.DataItem, "electronic_permission_icon", "help-circle") %>' class="align-text-bottom"
                     style='color:<%# SafeEval(Container.DataItem, "electronic_permission_icon_color", "yellow") %>;'></i>
                  </span>
                </ItemTemplate>
              </asp:TemplateField>   
              <asp:TemplateField HeaderText="Action">
                <HeaderStyle CssClass="text-center" />
                <ItemStyle HorizontalAlign="Center"/>
                <ItemTemplate>
                    <div style="width:<%# string.IsNullOrEmpty(SafeEval(Container.DataItem, "cc_id").ToString()) ? "100": "130" %>px">
                        <button type="button" value='<%# Eval("publication_id") %>' class="btn btn-light btn-pmhome btn-sm" 
                                onclick="getPrice(this.value, this)" 
                                >
                            Get Price
                         </button>                     
                     </div>
                    
                </ItemTemplate>
              </asp:TemplateField>
        </Columns> 
        <PagerTemplate>
           
        </PagerTemplate>
        </asp:GridView>
        <ul class="pagination justify-content-center" id="ulPagger" runat="server">
            <li class="page-item" id="liFirst" runat="server">
                <asp:LinkButton ID="lnkFirst" runat="server" CssClass="page-link" CommandName="Page" CommandArgument="First" OnClick="LnkFirst_Click">First</asp:LinkButton>
             </li>
            <li class="page-item" id="liPrev" runat="server">
                <asp:LinkButton ID="lnkPrev" runat="server" CssClass="page-link" CommandName="Page" CommandArgument="Prev" OnClick="LnkPrev_Click">Previous</asp:LinkButton>
             </li>
            <asp:Repeater ID="rptPages" OnItemDataBound="RptPages_ItemDataBound" runat="server">
                <ItemTemplate>
                    <li class="page-item" runat="server">
                        <asp:LinkButton ID="lnkPageNumber" CssClass="page-link" CommandName="Page" runat="server" OnClick="LnkPageNumber_Click" />
                    </li>
                </ItemTemplate>
            </asp:Repeater>
            <li class="page-item"id="liNext" runat="server">
                <asp:LinkButton ID="lnkNext" runat="server" CssClass="page-link" CommandName="Page" CommandArgument="Next" OnClick="LnkNext_Click">Next</asp:LinkButton>
            </li>
            <li class="page-item" id="liLast" runat="server">
                <asp:LinkButton ID="lnkLast" runat="server" CssClass="page-link" CommandName="Page" CommandArgument="Last" OnClick="LnkLast_Click">Last</asp:LinkButton>
             </li>
        </ul>
    </div>
    
    <br />
    <div class="row w-100 pl-3">
        <div class="form-group col-sm form-check-inline">
            <a href="https://marketplace.copyright.com/rs-ui-web/mp/terms" target="_blank">CCC Terms and Conditions</a>
        </div>
        <div class="form-group col-sm form-check-inline" id="estFields" runat="server">
            <span class="ml-auto mr-3">Parameters used for price estimates:</span>
            <label for="num_copies" class="mb-0 mr-1"># Copies</label>
            <input type="text" id="num_copies" style="width:3rem;" class="text-center form-control" value="1"/>
            <label for="num_pages" class="mb-0 mr-1 ml-2"># Pages</label>
            <input type="text" id="num_pages" style="width:3rem;" class="text-center form-control" value="1"/>
            <label for="chk_entire_book" class="mb-0 mr-1 ml-2">Entire Book</label>
            <input type="checkbox" id="chk_entire_book" style="width:1rem;" class="text-center form-control" value="1"/>
        </div>
    </div>
</asp:Content>

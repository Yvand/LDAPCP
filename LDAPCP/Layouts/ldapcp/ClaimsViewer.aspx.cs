using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Web;
using System.Web.UI.WebControls.WebParts;
using Microsoft.SharePoint;
using Microsoft.SharePoint.WebControls;

namespace ldapcp
{
    public partial class ClaimsViewer : LayoutsPageBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            ClaimsPrincipal principal = Thread.CurrentPrincipal as IClaimsPrincipal;
            IClaimsIdentity identity = principal.Identity as IClaimsIdentity;

            IList<ClaimEntity> list = new List<ClaimEntity>();

            foreach (Claim claim in identity.Claims)
               
                list.Add(new ClaimEntity()
                {
                    ClaimType = claim.ClaimType,
                    Value = claim.Value,
                    ValueType = claim.ValueType
                });

            RefreshGrid(list);
        }
        private void RefreshGrid(IList<ClaimEntity> list)
        {
            grid.DataSource = null;
            grid.DataSource = list;
            grid.DataBind();
        }
    }
}

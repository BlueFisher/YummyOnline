﻿using System.Web;
using System.Web.Security;

namespace YummyOnlineDAO.Identity {
	public class StaffSigninManager : BaseSigninManager {
		public StaffSigninManager(HttpContextBase httpCtx) : base(httpCtx) {

		}
		public void Signin(Models.Staff staff, bool isPersistent) {
			FormsAuthentication.SetAuthCookie(staff.Id.ToString(), isPersistent);
		}
	}
}

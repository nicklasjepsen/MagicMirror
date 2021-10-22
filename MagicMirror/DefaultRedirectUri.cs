﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicMirror
{
    internal class DefaultRedirectUri
    {
        /// <summary>
        /// Value of the redirect URI used for this application.
        /// Note it needs to be defined in a different file than MainPage.xaml.cs, because of the way 
        /// the portal replaces strings in the quickstarts when generating preconfigured projects.
        /// </summary>
        public static string Value { get; } = "https://login.microsoftonline.com/common/oauth2/nativeclient";
    }
}

﻿namespace WhoCanHelpMe.Web
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.Threading;
    using System.Web;
    using System.Web.Mvc;
    using System.Web.Routing;

    using Castle.Windsor;
    using Castle.Windsor.Configuration.Interpreters;

    using Code;

    using CommonServiceLocator.WindsorAdapter;

    using Controllers;

    using Elmah;

    using Framework.Container;
    using Framework.Extensions;
    using Framework.Traversal;

    using log4net;

    using Microsoft.Practices.ServiceLocation;

    using Registrars;

    using Infrastructure.NHibernate;
    using Infrastructure.NHibernateMaps;

    using SharpArch.Data.NHibernate;
    using SharpArch.Web.NHibernate;

    using NHibernateInitializer=Infrastructure.NHibernate.NHibernateInitializer;

    #endregion

    /// <summary>
    /// Represents the MVC Application
    /// </summary>
    /// <remarks>
    /// For instructions on enabling IIS6 or IIS7 classic mode, 
    /// visit http://go.microsoft.com/?LinkId=9394801
    /// </remarks>
    public class MvcApplication : System.Web.HttpApplication
    {
        private WebSessionStorage webSessionStorage;

        private static bool showCustomErrorPages;

        /// <summary>
        /// Due to issues on IIS7, the NHibernate initialization must occur in Init().
        /// But Init() may be invoked more than once; accordingly, we introduce a thread-safe
        /// mechanism to ensure it's only initialized once.
        /// See http://msdn.microsoft.com/en-us/magazine/cc188793.aspx for explanation details.
        /// </summary>
        public override void Init()
        {
            this.webSessionStorage = new WebSessionStorage(this);
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            // Only initialise the NHibernate Sessions for a non static, i.e. Controller Action request
            if (!this.Request.IsStaticFile())
            {
                NHibernateInitializer.Instance().Initialize(this.InitialiseNHibernateSessions);
            }

            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-GB");
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-GB");
        }

        protected void Application_Start()
        {
            IWindsorContainer container = InitializeServiceLocator();

            showCustomErrorPages = Convert.ToBoolean(ConfigurationManager.AppSettings["showCustomErrorPages"]);

            ComponentRegistrar.Register(container);
            ComponentInitialiser.Initialise();
        }

        protected void Application_Error()
        {
            // Use ELMAH to log the exception
            var exception = this.Server.GetLastError();

            var context = HttpContext.Current;

            var signal = ErrorSignal.FromContext(context);
            if (signal != null)
            {
                signal.Raise(exception, context);
            }

            //  Show custom error page if necessary
            if (showCustomErrorPages)
            {
                if (exception is HttpRequestValidationException)
                {
                    this.DisplayErrorPage("InvalidInput");
                    return;
                }

                this.DisplayErrorPage("Error");
            }
        }

        /// <summary>
        /// Returns a response by executing the Error controller with the specified action.
        /// </summary>
        /// <param name="action">
        /// The action.
        /// </param>
        private void DisplayErrorPage(string action)
        {
            var routeData = new RouteData();
            routeData.Values.Add("controller", "Error");
            routeData.Values.Add("action", action);

            this.Server.ClearError();
            this.Response.Clear();

            var httpContext = new HttpContextWrapper(this.Context);
            var requestContext = new RequestContext(httpContext, routeData);

            IController errorController =
                ControllerBuilder.Current.GetControllerFactory().CreateController(
                    new RequestContext(
                        httpContext,
                        routeData),
                    "Error");

            // Clear the query string, in particular to avoid HttpRequestValidationException being re-raised
            // when the error view is rendered by the Error Controller.
            httpContext.RewritePath(httpContext.Request.FilePath, httpContext.Request.PathInfo, string.Empty);

            errorController.Execute(requestContext);
        }

        private void InitialiseNHibernateSessions()
        {
            NHibernateSession.Init(
                webSessionStorage,
                new[] { Server.MapPath("~/bin/WhoCanHelpMe.Infrastructure.dll") },
                new AutoPersistenceModelGenerator().Generate(),
                Server.MapPath("~/Configuration/NHibernate/{0}.config").FormatWith(SessionKeys.Data));
        }

        private static IWindsorContainer InitializeServiceLocator()
        {
            IWindsorContainer container = new WindsorContainer(new XmlInterpreter());

            ServiceLocator.SetLocatorProvider(() => new WindsorServiceLocator(container));

            return container;
        }
    }
}
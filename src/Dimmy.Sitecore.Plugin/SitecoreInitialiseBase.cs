﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Reflection;
using Dimmy.Cli.Commands.Project.SubCommands;
using Dimmy.Engine.Commands;
using Dimmy.Engine.Commands.Project;
using Dimmy.Engine.Services;
using NuGet.Packaging;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace Dimmy.Sitecore.Plugin
{
    public abstract class SitecoreInitialiseBase<TArg> : InitialiseSubCommand
        where TArg : SitecoreInitialiseArgument, new()
    {
        protected readonly ICommandHandler<InitialiseProject> InitialiseProjectCommandHandler;
        public string TemplatePath
        {
            get
            {
                var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Join(assemblyPath, $"content/Versions/{Version}");
            }
        }
        protected abstract string Version { get; }

        private IEnumerable<string> Topologies
        {
            get
            {
                var templates = Directory.GetFiles(TemplatePath);

                foreach (var template in templates)
                {
                    yield return Path.GetFileName(template).Split(".")[0];
                }
            }
        }

        protected SitecoreInitialiseBase(
            ICommandHandler<InitialiseProject> initialiseProjectCommandHandler)
        {
            InitialiseProjectCommandHandler = initialiseProjectCommandHandler;
        }

        public override void HydrateCommand(Command command)
        {
            var arg = new TArg();
            
            command.AddOption(new Option<string>("--license-path", "Path to the Sitecore License"));
            command.AddOption(new Option<string>("--registry", $"Defaults to {arg.Registry}"));
            command.AddOption(new Option<string>("--topology", $"The Sitecore topology. Defaults to {arg.Topology}. Options: \n {string.Join('\n', Topologies)}"));
            
            arg.PrivateVariables = new Dictionary<string, string>();
            arg.PublicVariables = new Dictionary<string, string>();

            DoHydrateCommand(command, arg);
            command.Handler = CommandHandler.Create((TArg arg) => Initialise(arg));
        }

        private void Initialise(TArg arg)
        {
            arg.PrivateVariables.AddRange(new Dictionary<string, string>
            {
                {"MsSql.SaPassword", NonceService.Generate()},
                {"Sitecore.License", CreateEncodedSitecoreLicense(arg)},
                {"Sitecore.TelerikEncryptionKey", NonceService.Generate()},
            });
            
            arg.MetaData.Add("SitecoreVersion", Version);
            arg.MetaData.Add(Constants.MetaData.SitecoreTopology, arg.Topology);
            
            DoInitialise(arg);
        }

        protected abstract void DoHydrateCommand(Command command, TArg arg);
        protected abstract void DoInitialise(TArg arg);
        
        protected string CreateEncodedSitecoreLicense(TArg si)
        {
            var licenseBytes = File.ReadAllBytes(si.LicensePath);
            using var licenseMemoryStream = new MemoryStream();
            using var licenseGZipStream = new GZipStream(licenseMemoryStream, CompressionMode.Compress);
            licenseGZipStream.Write(licenseBytes, 0, licenseBytes.Length);
            licenseGZipStream.Close();
            licenseMemoryStream.Close();

            var sitecoreLicense = Convert.ToBase64String(licenseMemoryStream.ToArray());
            return sitecoreLicense;
        }
        
        
    }
}
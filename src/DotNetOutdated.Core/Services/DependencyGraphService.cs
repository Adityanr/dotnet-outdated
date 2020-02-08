﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Xml.Linq;
using DotNetOutdated.Core.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.ProjectModel;

namespace DotNetOutdated.Core.Services
{
    /// <remarks>
    /// Credit for the stuff happening in here goes to the https://github.com/jaredcnance/dotnet-status project
    /// </remarks>
    public class DependencyGraphService : IDependencyGraphService
    {
        private readonly IDotNetRunner _dotNetRunner;
        private readonly IFileSystem _fileSystem;

        public DependencyGraphService(IDotNetRunner dotNetRunner, IFileSystem fileSystem)
        {
            _dotNetRunner = dotNetRunner;
            _fileSystem = fileSystem;
        }
        
        public DependencyGraphSpec GenerateDependencyGraph(string projectPath)
        {
            if (string.Equals(_fileSystem.Path.GetExtension(projectPath), ".sln", StringComparison.OrdinalIgnoreCase))
            {
                return GenerateSolutionDependencyGraph(projectPath);
            }

            string dgOutput = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), _fileSystem.Path.GetTempFileName());
                
            string[] arguments = {"msbuild", $"\"{projectPath}\"", "/t:Restore,GenerateRestoreGraphFile", $"/p:RestoreGraphOutputPath=\"{dgOutput}\""};

            var runStatus = _dotNetRunner.Run(_fileSystem.Path.GetDirectoryName(projectPath), arguments);

            if (runStatus.IsSuccess)
            {
                string dependencyGraphText = _fileSystem.File.ReadAllText(dgOutput);
                return new DependencyGraphSpec(JsonConvert.DeserializeObject<JObject>(dependencyGraphText));
            }
            else
            {
                throw new CommandValidationException($"Unable to process the project `{projectPath}. Are you sure this is a valid .NET Core or .NET Standard project type?" +
                                                     $"\r\n\r\nHere is the full error message returned from the Microsoft Build Engine:\r\n\r\n" + runStatus.Output);
            }
        }

        /// <summary>
        /// Extracts list of projects from solution file and generates dependency graph only for Microsoft SDK projects.
        /// Non Microsoft .SDK projects are ignored, because those are not supported by .NET Core MSBuild.
        /// </summary>
        private DependencyGraphSpec GenerateSolutionDependencyGraph(string solutionPath)
        {
            string dgOutput = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), _fileSystem.Path.GetTempFileName());
            string[] arguments = { "msbuild", $"\"{solutionPath}\"", "/t:Restore,GenerateRestoreGraphFile", $"/p:RestoreGraphOutputPath=\"{dgOutput}\"" };

            string directoryPath = _fileSystem.Path.GetDirectoryName(solutionPath);

            var runStatus = _dotNetRunner.Run(directoryPath, arguments);
            
            if (runStatus.IsSuccess)
            {
                string dependencyGraphText = _fileSystem.File.ReadAllText(dgOutput);
                return new DependencyGraphSpec(JsonConvert.DeserializeObject<JObject>(dependencyGraphText));
            }
            else
            {
                throw new CommandValidationException($"Unable to read the solution '{solutionPath}'.\r\n\r\nHere is the full error message returned from the dotnet:\r\n\r\n{runStatus.Output}");
            }
        }
    }
}
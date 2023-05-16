﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using Microsoft.SemanticKernel.Skills.Web;
using Planning.IterativePlanner;
using SemanticKernel.IntegrationTests.Fakes;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Planning.Iterative;

public sealed class IterativePlannerTests : IDisposable
{
    public IterativePlannerTests(ITestOutputHelper output)
    {
        this._logger = NullLogger.Instance;
        this._testOutputHelper = new RedirectOutput(output);

        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<IterativePlannerTests>()
            .Build();

        string? bingApiKeyCandidate = this._configuration["Bing:ApiKey"];
        Assert.NotNull(bingApiKeyCandidate);
        this._bingApiKey = bingApiKeyCandidate;
    }

    [Fact]
    public async Task CanCorrectlyParseLongConversationAsync()
    {
        // the purpose of this test it to ensure that even for long conversations
        // the result is still generated correctly and it can be properly parsed

        // Arrange
        IKernel kernel = this.InitializeKernel();
        //lets limit it to 10 steps to have a long chain and scratchpad
        var plan = new IterativePlanner(kernel, 12);
        var goal = "count down from 10 to one using subtraction functionality of the calculator tool, and decrementing value 1 by 1 ";
        var result = await plan.ExecutePlanAsync(goal);

        // Assert
        this.PrintPlan(plan, result);
        //there should be text final in the result
        Assert.Contains("1", result);
        //there should be exactly 10 steps
        Assert.Equal(10, plan.Steps.Count);
    }

    private void PrintPlan(IterativePlanner plan, string result)
    {
        foreach (var step in plan.Steps)
        {
            this._testOutputHelper.WriteLine("a: " + step.Action);
            this._testOutputHelper.WriteLine("ai: " + step.ActionInput);
            this._testOutputHelper.WriteLine("t: " + step.Thought);
            this._testOutputHelper.WriteLine("o: " + step.Observation);
            this._testOutputHelper.WriteLine("--");
        }

        this._testOutputHelper.WriteLine(result);
    }

    [Fact]
    public async Task CanExecuteSimpleIterativePlanAsync()
    {
        // Arrange
        IKernel kernel = this.InitializeKernel();
        //lets limit it to only 2 steps, which should be 2 searches
        var plan = new IterativePlanner(kernel, 2);
        var goal = "Who is Leo DiCaprio's girlfriend? What is her current age raised to the 0.43 power?";

        // Act
        var result = await plan.ExecutePlanAsync(goal);
        this._testOutputHelper.WriteLine(result);

        // Debug and show all the steps and actions
        this.PrintPlan(plan, result);

        // Assert
        // first step should be a search for girlfriend
        var firstStep = plan.Steps[0];
        Assert.Equal("Search", firstStep.Action);
        Assert.Contains("girlfriend", firstStep.Thought);

        // second step should be a search for age of the girlfriend
        //var secondStep = plan.Steps[1];
        //Assert.Equal("Search", secondStep.Action);
        //Assert.Contains("age", secondStep.Thought);
    }

    private IKernel InitializeKernel()
    {
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        AzureOpenAIConfiguration? azureOpenAIEmbeddingsConfiguration = this._configuration.GetSection("AzureOpenAIEmbeddings").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIEmbeddingsConfiguration);

        var builder = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureTextCompletionService(
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
            });

        //var builder = Kernel.Builder
        //    .WithLogger(this._logger)
        //    .Configure(config =>
        //    {
        //        config.AddAzureChatCompletionService(
        //            //deploymentName: azureOpenAIConfiguration.DeploymentName,
        //            deploymentName: "gpt-35-turbo",
        //            endpoint: azureOpenAIConfiguration.Endpoint,
        //            apiKey: azureOpenAIConfiguration.ApiKey);
        //    });

        var kernel = builder.Build();

        BingConnector connector = new(this._bingApiKey);

        var webSearchEngineSkill = new WebSearchEngineSkill(connector);

        kernel.ImportSkill(webSearchEngineSkill, "WebSearch");

        kernel.ImportSkill(new LanguageCalculatorSkill(kernel), "calculator");

        return kernel;
    }

    private readonly ILogger _logger;
    private readonly RedirectOutput _testOutputHelper;
    private readonly IConfigurationRoot _configuration;
    private string _bingApiKey;

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~IterativePlannerTests()
    {
        this.Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (this._logger is IDisposable ld)
            {
                ld.Dispose();
            }

            this._testOutputHelper.Dispose();
        }
    }
}

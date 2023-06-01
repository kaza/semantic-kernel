﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using Planning.IterativePlanner;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Planning.Iterative;

public sealed class MrklPlannerTextTests : IDisposable
{
    public MrklPlannerTextTests(ITestOutputHelper output)
    {
        this._logger = new RedirectOutput(output);
        this._testOutputHelper = output;

        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<MrklPlannerTextTests>()
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
        var plan = new MrklPlannerText(kernel, 12);
        var goal = "count down from 10 to one using subtraction functionality of the calculator tool, and decrementing value 1 by 1 ";
        var result = await plan.ExecutePlanAsync(goal);

        // Assert
        this.PrintPlan(plan, result);
        //there should be text final in the result
        Assert.Contains("1", result);
        //there should be exactly 10 steps
        Assert.Equal(10, plan.Steps.Count);
    }

    private void PrintPlan(MrklPlannerText plan, string result)
    {
        foreach (var step in plan.Steps)
        {
            this._testOutputHelper.WriteLine("a: " + step.Action);
            this._testOutputHelper.WriteLine("ai: " + step.ActionInput);
            this._testOutputHelper.WriteLine("t: " + step.Thought);
            this._testOutputHelper.WriteLine("o: " + step.Observation);
            this._testOutputHelper.WriteLine("--");
        }

        this._testOutputHelper.WriteLine("Result:" + result);
    }

    [Fact]
    public async Task CanExecuteSimpleIterativePlanAsync()
    {
        // Arrange
        IKernel kernel = this.InitializeKernel();
        //it should be able to finish in 4 steps 
        var plan = new MrklPlannerText(kernel, 4);
        var goal = "Who is Leo DiCaprio's girlfriend? What is her current age raised to the 0.43 power?";
        //var goal = "Who is Leo DiCaprio's girlfriend? What is her current age ?";

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
        var secondStep = plan.Steps[1];
        Assert.Equal("Search", secondStep.Action);
        Assert.Contains("age", secondStep.Thought);

        var thirdStep = plan.Steps[2];
        Assert.Equal("Calculator", thirdStep.Action);
        Assert.Contains("power", thirdStep.Thought);
    }

    private IKernel InitializeKernel()
    {
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        var builder = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureTextCompletionService(
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
            });

        var kernel = builder.Build();

        BingConnector connector = new(this._bingApiKey);

        var webSearchEngineSkill = new WebSearchSkill(connector);

        kernel.ImportSkill(webSearchEngineSkill, "WebSearch");

        kernel.ImportSkill(new LanguageCalculatorSkill(kernel), "calculator");

        return kernel;
    }

    private readonly RedirectOutput _logger;
    private readonly ITestOutputHelper _testOutputHelper;

    private readonly IConfigurationRoot _configuration;
    private string _bingApiKey;

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~MrklPlannerTextTests()
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

            this._logger.Dispose();
        }
    }
}
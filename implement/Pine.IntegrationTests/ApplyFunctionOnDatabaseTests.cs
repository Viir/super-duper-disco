using ElmTime;
using AwesomeAssertions;
using Pine.Core;
using Pine.Core.Elm;
using Pine.Core.Json;
using Pine.Elm.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Pine.IntegrationTests;

public class ApplyFunctionOnDatabaseTests
{
    [Fact]
    public async Task Apply_exposed_function_via_admin_interface_adding_to_counter_web_app()
    {
        var adminPassword = "test";

        using var testSetup = WebHostAdminInterfaceTestSetup.Setup(
            deployAppAndInitElmState: ElmWebServiceAppTests.CounterWebApp,
            adminPassword: adminPassword);

        var expectedCounter = 0;

        using (var server = testSetup.StartWebHost())
        {
            using var publicClient = testSetup.BuildPublicAppHttpClient();

            using var adminClient = testSetup.BuildAdminInterfaceHttpClient();

            testSetup.SetDefaultRequestHeaderAuthorizeForAdmin(adminClient);

            {
                var additionViaPublicInterface = 1234;

                var httpResponse =
                    await publicClient.PostAsync("", JsonContent.Create(new { addition = additionViaPublicInterface }));

                var httpResponseContent = await httpResponse.Content.ReadAsStringAsync();

                expectedCounter += additionViaPublicInterface;

                httpResponseContent.Should().Be(expectedCounter.ToString(), "response content should match expected counter");
            }

            {
                var applyFunctionResponse =
                    await adminClient.PostAsJsonAsync(
                        ElmTime.Platform.WebService.StartupAdminInterface.PathApiApplyDatabaseFunction,
                        new ElmTime.AdminInterface.ApplyDatabaseFunctionRequest(
                            functionName: "Backend.ExposeFunctionsToAdmin.addToCounter",
                            serializedArgumentsJson: [17.ToString()],
                            commitResultingState: false));

                ((int)applyFunctionResponse.StatusCode).Should().Be(200, "applyFunctionResponse.StatusCode should be OK");
            }

            {
                var httpResponse = await publicClient.PostAsync("", JsonContent.Create(new { addition = 0 }));

                var httpResponseContent = await httpResponse.Content.ReadAsStringAsync();

                httpResponseContent.Should().Be(expectedCounter.ToString(), "response content should match expected counter");
            }

            {
                var additionViaAdminInterface = 4567;

                var applyFunctionResponse =
                    await adminClient.PostAsJsonAsync(
                        ElmTime.Platform.WebService.StartupAdminInterface.PathApiApplyDatabaseFunction,
                        new ElmTime.AdminInterface.ApplyDatabaseFunctionRequest(
                            functionName: "Backend.ExposeFunctionsToAdmin.addToCounter",
                            serializedArgumentsJson: [additionViaAdminInterface.ToString()],
                            commitResultingState: true));

                expectedCounter += additionViaAdminInterface;

                ((int)applyFunctionResponse.StatusCode).Should().Be(200, "applyFunctionResponse.StatusCode should be OK");
            }

            {
                var httpResponse = await publicClient.PostAsync("", JsonContent.Create(new { addition = 0 }));

                var httpResponseContent = await httpResponse.Content.ReadAsStringAsync();

                httpResponseContent.Should().Be(expectedCounter.ToString(), "response content should match expected counter");
            }
        }

        using (var server = testSetup.StartWebHost())
        {
            using var publicClient = testSetup.BuildPublicAppHttpClient();

            {
                var httpResponse = await publicClient.PostAsync("", JsonContent.Create(new { addition = 0 }));

                var httpResponseContent = await httpResponse.Content.ReadAsStringAsync();

                httpResponseContent.Should().Be(expectedCounter.ToString(), "response content should match expected counter");
            }
        }
    }

    [Fact]
    public async Task Apply_function_via_admin_interface_report_from_calculator()
    {
        var adminPassword = "test";

        using var testSetup = WebHostAdminInterfaceTestSetup.Setup(
            deployAppAndInitElmState: ElmWebServiceAppTests.CalculatorWebApp,
            adminPassword: adminPassword);

        var expectedResultingNumber = 0;

        using var server = testSetup.StartWebHost();

        using var publicClient = testSetup.BuildPublicAppHttpClient();

        using var adminClient = testSetup.BuildAdminInterfaceHttpClient();

        testSetup.SetDefaultRequestHeaderAuthorizeForAdmin(adminClient);

        {
            var additionViaPublicInterface = 1234;

            var httpResponse =
                await publicClient.PostAsync(
                    "",
                    JsonContent.Create<CalculatorOperation>(new CalculatorOperation.AddOperation(additionViaPublicInterface)));

            var httpResponseContent = await httpResponse.Content.ReadAsStringAsync();

            expectedResultingNumber += additionViaPublicInterface;

            JsonSerializer.Deserialize<CalculatorBackendStateRecord>(httpResponseContent).Should().BeEquivalentTo(
                new CalculatorBackendStateRecord(
                    httpRequestCount: 1,
                    operationsViaHttpRequestCount: 1,
                    resultingNumber: expectedResultingNumber),
                "response content should match expected state");
        }

        {
            var applyFunctionResponse =
                await adminClient.PostAsJsonAsync(
                    ElmTime.Platform.WebService.StartupAdminInterface.PathApiApplyDatabaseFunction,
                    new ElmTime.AdminInterface.ApplyDatabaseFunctionRequest(
                        functionName: "Backend.ExposeFunctionsToAdmin.customUsageReport",
                        serializedArgumentsJson: [JsonSerializer.Serialize("Hello world")],
                        commitResultingState: false));

            var applyFunctionResponseContent = await applyFunctionResponse.Content.ReadAsStringAsync();

            ((int)applyFunctionResponse.StatusCode).Should().Be(200, "applyFunctionResponse.StatusCode should be OK");

            var applyFunctionResponseStruct =
                JsonSerializer.Deserialize<Result<string, ElmTime.AdminInterface.ApplyDatabaseFunctionSuccess>>(
                    applyFunctionResponseContent)
                ??
                throw new Exception("applyFunctionResponseContent is null");

            var applyFunctionSuccess =
                applyFunctionResponseStruct
                .Extract(fromErr: err => throw new Exception(err));

            var resultLessStateJson =
                applyFunctionSuccess.functionApplicationResult.resultLessStateJson
                .ToResult("resultLessStateJson is Nothing")
                .Extract(fromErr: err => throw new Exception(err));

            var customReport = JsonSerializer.Deserialize<CustomUsageReportStruct>(resultLessStateJson);

            customReport.Should().BeEquivalentTo(
                new CustomUsageReportStruct(
                    httpRequestCount: 1,
                    operationsViaHttpRequestCount: 1,
                    anotherField: "Custom content from argument: Hello world"));
        }
    }

    [Fact]
    public async Task List_exposed_functions_via_admin_interface()
    {
        var adminPassword = "test";

        using var testSetup = WebHostAdminInterfaceTestSetup.Setup(
            deployAppAndInitElmState: ElmWebServiceAppTests.CalculatorWebApp,
            adminPassword: adminPassword);

        using var server = testSetup.StartWebHost();

        using var adminClient = testSetup.BuildAdminInterfaceHttpClient();

        testSetup.SetDefaultRequestHeaderAuthorizeForAdmin(adminClient);

        var listFunctionsResponse =
            await adminClient.GetAsync(ElmTime.Platform.WebService.StartupAdminInterface.PathApiListDatabaseFunctions);

        var listFunctionsResponseContent = await listFunctionsResponse.Content.ReadAsStringAsync();

        ((int)listFunctionsResponse.StatusCode).Should().Be(200, "listFunctionsResponse.StatusCode");

        var listFunctionsResponseStruct =
            JsonSerializer.Deserialize<Result<string, IReadOnlyList<ElmTime.StateShim.InterfaceToHost.NamedExposedFunction>>>(
                listFunctionsResponseContent)
            ??
            throw new Exception("listFunctionsResponseContent is null");

        var functionsDescriptions =
            listFunctionsResponseStruct
            .Extract(fromErr: err => throw new Exception(err));

        ElmTime.StateShim.InterfaceToHost.ExposedFunctionDescription? functionDescriptionFromName(string functionName) =>
            functionsDescriptions
            ?.FirstOrDefault(functionDescription => functionDescription.functionName == functionName)
            ?.functionDescription;

        var applyCalculatorOperationFunctionDescription =
            functionDescriptionFromName("Backend.ExposeFunctionsToAdmin.applyCalculatorOperation");

        var customUsageReportFunctionDescription =
            functionDescriptionFromName("Backend.ExposeFunctionsToAdmin.customUsageReport");

        applyCalculatorOperationFunctionDescription.Should().NotBeNull();
        customUsageReportFunctionDescription.Should().NotBeNull();

        applyCalculatorOperationFunctionDescription!.returnType.Should().BeEquivalentTo(
            new ElmTime.StateShim.InterfaceToHost.ExposedFunctionReturnTypeDescription(
                sourceCodeText: "Backend.State.State",
                containsAppStateType: true));

        applyCalculatorOperationFunctionDescription!.parameters.Should().BeEquivalentTo(
            [
                new ElmTime.StateShim.InterfaceToHost.ExposedFunctionParameterDescription(
                    patternSourceCodeText: "operation",
                    typeSourceCodeText: "Calculator.CalculatorOperation",
                    typeIsAppStateType: false),
                new ElmTime.StateShim.InterfaceToHost.ExposedFunctionParameterDescription(
                    patternSourceCodeText: "stateBefore",
                    typeSourceCodeText: "Backend.State.State",
                    typeIsAppStateType: true)
            ]);

        customUsageReportFunctionDescription!.returnType.Should().BeEquivalentTo(
            new ElmTime.StateShim.InterfaceToHost.ExposedFunctionReturnTypeDescription(
                sourceCodeText: "CustomUsageReport",
                containsAppStateType: false));

        customUsageReportFunctionDescription!.parameters.Should().BeEquivalentTo(
            [
                new ElmTime.StateShim.InterfaceToHost.ExposedFunctionParameterDescription(
                    patternSourceCodeText: "customArgument",
                    typeSourceCodeText: "String",
                    typeIsAppStateType: false),
                new ElmTime.StateShim.InterfaceToHost.ExposedFunctionParameterDescription(
                    patternSourceCodeText: "state",
                    typeSourceCodeText: "Backend.State.State",
                    typeIsAppStateType: true),
            ]);
    }


    [Fact]
    public void List_exposed_functions_sandbox()
    {
        var appConfigTree =
            PineValueComposition.ParseAsTreeWithStringPath(ElmWebServiceAppTests.CalculatorWebApp)
            .Extract(err => throw new Exception("Failed parsing app source files as tree: " + err));

        var webServiceConfig =
            WebServiceInterface.ConfigFromSourceFilesAndEntryFileName(
                appConfigTree,
                ["src", "Backend", "Main.elm"]);

        var pineVM = new PineVM.PineVM();

        var webServiceApp =
            new MutatingWebServiceApp(webServiceConfig);

        var exposedFunctions = webServiceConfig.JsonAdapter.ExposedFunctions;

        var exposedFunctionApplyCalculatorOp =
            exposedFunctions
            .FirstOrDefault(exposedFunction =>
            exposedFunction.Key is "Backend.ExposeFunctionsToAdmin.applyCalculatorOperation")
            .Value;

        exposedFunctionApplyCalculatorOp.Should().NotBeNull(
            "Exposed function 'applyCalculatorOperation' not found in the app.");

        exposedFunctionApplyCalculatorOp.Description.Should().BeEquivalentTo(
            new ElmTimeJsonAdapter.ExposedFunctionDescription(
                ReturnType:
                new ElmTimeJsonAdapter.ExposedFunctionDescriptionReturnType(
                    SourceCodeText: "Backend.State.State",
                    ContainsAppStateType: true),
                Parameters:
                [
                    new ElmTimeJsonAdapter.ExposedFunctionDescriptionParameter(
                        PatternSourceCodeText: "operation",
                        TypeSourceCodeText: "Calculator.CalculatorOperation",
                        TypeIsAppStateType: false),

                    new ElmTimeJsonAdapter.ExposedFunctionDescriptionParameter(
                        PatternSourceCodeText: "stateBefore",
                        TypeSourceCodeText: "Backend.State.State",
                        TypeIsAppStateType: true)
                ]));

        var exposedFunctionCustomUsageReport =
            exposedFunctions
            .FirstOrDefault(exposedFunction =>
            exposedFunction.Key is "Backend.ExposeFunctionsToAdmin.customUsageReport")
            .Value;

        exposedFunctionCustomUsageReport.Should().NotBeNull(
            "Exposed function 'customUsageReport' not found in the app.");


        {
            /*
            type CalculatorOperation
                = AddOperation Int


            applyCalculatorOperation : CalculatorOperation -> Int -> Int
            applyCalculatorOperation operation numberBefore =
                case operation of
                    AddOperation addition ->
                        numberBefore + addition
             * */

            var applyAdditionArgument =
                ElmValue.TagInstance("AddOperation", [ElmValue.Integer(1234)]);

            var applyResult =
                ElmTimeJsonAdapter.ApplyExposedFunction(
                    webServiceApp.AppState,
                    [applyAdditionArgument],
                    exposedFunctionApplyCalculatorOp,
                    pineVM: pineVM,
                    posixTimeMilli: 0);

            if (applyResult.IsErrOrNull() is { } err)
            {
                throw new Exception("Failed applying exposed function: " + err);
            }

            if (applyResult.IsOkOrNull() is not { } applyOk)
            {
                throw new Exception(
                    "Unexpected result type applying exposed function: " + applyResult);
            }

            var resultingState = applyOk.AppState;

            if (resultingState is null)
            {
                throw new Exception("Resulting state is null.");
            }

            var parseStateAsElmValue =
                ElmValueEncoding.PineValueAsElmValue(
                    resultingState,
                    null,
                    null)
                .Extract(err => throw new Exception("Failed encoding state as Elm value: " + err));

            ElmValue.RenderAsElmExpression(parseStateAsElmValue).expressionString.Should().Be(
                "{ httpRequestCount = 0, operationsViaHttpRequestCount = 0, resultingNumber = 1234 }",
                "Resulting app state should match expected format");
        }
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(JsonConverterForChoiceType))]
    private abstract record CalculatorOperation
    {
        public record AddOperation(int Operand)
            : CalculatorOperation;
    }

    private record CalculatorBackendStateRecord(
        int httpRequestCount,
        int operationsViaHttpRequestCount,
        int resultingNumber);

    private record CustomUsageReportStruct(
        int httpRequestCount,
        int operationsViaHttpRequestCount,
        string anotherField);
}

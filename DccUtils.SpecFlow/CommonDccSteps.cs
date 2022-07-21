using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dcc.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using TechTalk.SpecFlow;

namespace Dcc.SpecFlow;

[Binding]
public class CommonDccSteps : ContextScenario {

    [Given(@"для инициализации ""(.*)""\.""(.*)"" используется тип ""(.*)""")]
    public void UseSpecificTypeWhenInitializingPropertyFromTable(string targetTypeName, string propertyPath, string specificTypeForProperty) {
        var target = GetTypeByName(targetTypeName);
        target.Should().NotBeNull();

        var specificType = GetTypeByName(specificTypeForProperty);
        specificType.Should().NotBeNull();
        specificType.Should().NotBeAbstract();
        specificType.Should().HaveDefaultConstructor();

        var property = target!.GetPropertyPath(propertyPath).Last();

        specificType.Should().NotBe(property.PropertyType);
        specificType.Should().BeAssignableTo(property.PropertyType);

        var mapper = Get<PropertyInstanceTypeMapper>();
        mapper.Map(property, specificType!);
    }

    [Given(@"""(.*)"" содержит")]
    public void GivenСодержит(string typeName, Table table) => Set(typeName, table);

    [Then(@"""(.*)""\.""(.*)"" имеет тип ""(.*)""")]
    public void HasType(string targetTypeName, string propertyPath, string propertyValueTypeName) {
        var target = GetByTypeName(targetTypeName);
        var property = target.GetPropertyPath(propertyPath).Last();
        property.Should().NotBeNull();

        var value = target.GetPropertyValue(propertyPath);
        value.Should().NotBeNull();

        var expectedType = GetTypeByName(propertyValueTypeName);
        value.Should().BeOfType(expectedType);
    }

    [Then(@"""(.*)""\.""(.*)"" содержит")]
    public void ThenСодержит(string targetTypeName, string propertyPath, Table table) {
        var target = GetByTypeName(targetTypeName);
        var property = target.GetPropertyPath(propertyPath).Last();
        property.Should().NotBeNull();

        var value = target.GetPropertyValue(propertyPath);
        value.Should().NotBeNull();

        if (value is IEnumerable enumerable) {
            enumerable.Cast<object>().Any(item => {
                try {
                    ShouldBeAsTable(item, table);
                    return true;
                }
                catch {
                    return false;
                }
            }).Should().BeTrue();
            return;
        }

        ShouldBeAsTable(value, table);
    }

    [Given(@"""(.*)""\.""(.*)"" имеет ""(.*)"", содержащий")]
    public void GivenИмеетСодержащий(string targetTypeName, string propertyPath, string valueTypeName, Table table) {
        var target = GetByTypeName(targetTypeName);
        var property = target.GetPropertyPath(propertyPath).Last();
        property.Should().NotBeNull();

        var value = target.GetPropertyValue(propertyPath);

        if (value?.GetType().IsAssignableTo(typeof(IList)) == true || property.PropertyType.IsAssignableTo(typeof(IList))) {
            if (value == null) {
                if (propertyPath.Contains('.'))
                    throw new NotImplementedException("Инициализация вложенных пропертей пока не реализована.\n" +
                                                      "Советую проинициализировать путь к проперте руками (в отдельных тестовых шагах например), " +
                                                      "а саму пропертю-коллекцию - дефолтным листом");

                property.SetValue(target, Activator.CreateInstance(property.PropertyType));
            }

            var collection = (IList) target.GetPropertyValue(propertyPath)!;
            collection.Add(GetFromTable(valueTypeName, table));
            return;
        }


        if (propertyPath.Contains('.'))
            throw new NotImplementedException("Инициализация вложенных пропертей пока не реализована.\n" +
                                              "Советую проинициализировать путь к проперте руками (в отдельных тестовых шагах например)");

        property.SetValue(target, GetFromTable(valueTypeName, table));
    }



    [Given(@"в ответ на ""(.*)"" c корреляцией по ""(.*)"" (.*) возвращает ""(.*)"" и ""(.*)"", содержащий")]
    public void GivenHttpClientHandlerMockВОтветНаВозвращаетСодержащий(string requestTypeName, string correlationPropName, string correlationValue,  HttpStatusCode statusCode, string responseTypeName, Table table) {
        var requestType = GetTypeByName(requestTypeName);
        var mock = Get<HttpClientHandlerMock>();
        mock.SetFutureAnswer(new() {
            RequestFilter = x => x.Content!.ReadFromJsonAsync(requestType!).GetPropertyValue(correlationPropName)?.ToString()?.Equals(correlationValue, StringComparison.InvariantCultureIgnoreCase) == true,
            GetResponseMessage = async () => new(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(GetFromTable(responseTypeName, table), SerializerOptions))
            }
        });
    }

    [Given(@"в ответ на rest-запрос ""(.*)"" ""(.*)"" возвращается ""(.*)"" и ""(.*)"", содержащий")]
    public void GivenHttpClientHandlerMockВОтветНаВозвращаетСодержащий(string method, string url, HttpStatusCode statusCode, string responseTypeName, Table table) {
        var mock = Get<HttpClientHandlerMock>();
        mock.SetFutureAnswer(new() {
            RequestFilter = x => x.Method.Method == method && x.RequestUri == new Uri(url),
            GetResponseMessage = async () => new(statusCode) {
                Content = new StringContent(JsonSerializer.Serialize(GetFromTable(responseTypeName, table), SerializerOptions))
            }
        });
    }


    [Given(@"в ответ на rest-запрос ""(.*)"" ""(.*)"" возвращается ""(.*)"" и json")]
    public void GivenВОтветНаRestЗапросВозвращаетсяИJson(string method, string url, HttpStatusCode statusCode, Table table) {
        var mock = Get<HttpClientHandlerMock>();
        mock.SetFutureAnswer(new() {
            RequestFilter = x => x.Method.Method == method && x.RequestUri == new Uri(url),
            GetResponseMessage = () => Task.FromResult<HttpResponseMessage>(new(statusCode) {
                Content = new StringContent(table.Rows[0][0])
            })
        });
    }


    [Then(@"в ответ на ""(.*)"" c корреляцией по ""(.*)"" (.*) возвращает ""(.*)"" и ""(.*)"", содержащий")]
    [When(@"в ответ на ""(.*)"" c корреляцией по ""(.*)"" (.*) возвращает ""(.*)"" и ""(.*)"", содержащий")]
    public async Task ThenHttpClientHandlerMockВОтветНаВозвращаетСодержащий(string requestTypeName, string correlationPropName, string correlationValue,  HttpStatusCode statusCode, string responseTypeName, Table table) {
        var requestType = GetTypeByName(requestTypeName);
        var mock = Get<HttpClientHandlerMock>();
        await mock.Answer(new() {
            RequestFilter = x => x.Content!.ReadFromJsonAsync(requestType!).GetPropertyValue(correlationPropName)?.ToString()?.Equals(correlationValue, StringComparison.InvariantCultureIgnoreCase) == true,
            GetResponseMessage = async () => new(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(GetFromTable(responseTypeName, table), SerializerOptions))
            }
        });
    }

    [Then(@"в ответ на rest-запрос ""(.*)"" ""(.*)"" возвращается ""(.*)"" и ""(.*)"", содержащий")]
    [When(@"в ответ на rest-запрос ""(.*)"" ""(.*)"" возвращается ""(.*)"" и ""(.*)"", содержащий")]
    public async Task ThenHttpClientHandlerMockВОтветНаВозвращаетСодержащий(string method, string url, HttpStatusCode statusCode, string responseTypeName, Table table) {
        var mock = Get<HttpClientHandlerMock>();
        await mock.Answer(new() {
            RequestFilter = x => x.Method.Method == method && x.RequestUri == new Uri(url),
            GetResponseMessage = async () => new(statusCode) {
                Content = new StringContent(JsonSerializer.Serialize(GetFromTable(responseTypeName, table), SerializerOptions))
            }
        });
    }

    [Then(@"rest-запрос ""(.*)"" ""(.*)"" имеет тип контента application/x-www-form-urlencoded и содержит следующие значения")]
    public async Task ThenRestЗапросИмеетТипКонтентаApplicationXWwwFormUrlencodedИСодержитСледующиеЗначения(string method, string url, Table table) {
        var mock = Get<HttpClientHandlerMock>();
        var request = await mock.WaitRequest(x => x.Method.Method == method && x.RequestUri == new Uri(url), new CancellationTokenSource(Timeout).Token);
        request.Should().NotBeNull();
        request!.Content.Should().NotBeNull();
        request.Content!.Headers.ContentType.Should().NotBeNull();
        request.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");

        var content = await request.Content!.ReadAsStringAsync();
        var sentValues = QueryHelpers.ParseQuery(content);

        foreach (var expected in table.AsNameValueCollection(x => x[0], x => x[1])) {
            if (string.IsNullOrWhiteSpace(expected.Value)) {
                sentValues.Should().NotContainKey(expected.Key);
                sentValues.ContainsKey(expected.Key).Should().BeFalse();
                continue;
            }

            sentValues.Should().ContainKey(expected.Key);
            sentValues[expected.Key].ToString().Should().Be(expected.Value);
        }
    }


    [When(@"возвращается ""(.*)"", содержащий")]
    [Then(@"возвращается ""(.*)"", содержащий")]
    public void ThenПолученУспешныйОтветСодержащий(string typeName, Table table) {
        var response = GetByTypeName(typeName);
        ShouldBeAsTable(response, table);
    }


    [When(@"у ""(.*)"" запрашивается метод ""(.*)"" с ""(.*)"", содержащим")]
    [Then(@"у ""(.*)"" запрашивается метод ""(.*)"" с ""(.*)"", содержащим")]
    public async Task WhenУBackOfficePaymentApiClientЗапрашиваетсяМетодС(string targetTypeName, string methodName, string requestType, Table table) {
        var target = GetByTypeName(targetTypeName);
        var request = GetFromTable(requestType, table);

        var method = FindMethod(target.GetType(), request.GetType(), methodName);

        var responseTask = Invoke(method, target, request);
        await responseTask; //todo: use cancellation token
        var result = (object) ((dynamic) responseTask).Result;
        Set(result);
    }

    [When(@"у ""(.*)"" запрашивается метод ""(.*)"" с ""(.*)""")]
    [Then(@"у ""(.*)"" запрашивается метод ""(.*)"" с ""(.*)""")]
    public async Task WhenУЗапрашиваетсяМетодС(string targetTypeName, string methodName, string requestType) {
        var target = GetByTypeName(targetTypeName);
        var request = GetByTypeName(requestType);

        var method = FindMethod(target.GetType(), request.GetType(), methodName);

        var responseTask = Invoke(method, target, request);
        await responseTask;
        var result = (object) ((dynamic) responseTask).Result;
        Set(result);
    }

}

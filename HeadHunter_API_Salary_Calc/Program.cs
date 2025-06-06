using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

class Program
{
    static void Main()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "C# console program");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        List<decimal> salaries = new List<decimal>();
        string desiredCurrency = "RUR"; // Или "RUB"
        string searchText = Uri.EscapeDataString("C#");

        int currentPage = 0;
        int totalPages = 1;
        int foundVacancies = 0;
        int processedVacanciesWithSalary = 0;

        Console.WriteLine($"Поиск вакансий '{searchText}' с зарплатой в {desiredCurrency}...");

        string baseParams = $"text={searchText}&per_page=100";


        while (currentPage < totalPages)
        {
            string url = $"https://api.hh.ru/vacancies?{baseParams}&page={currentPage}";
            Console.WriteLine($"Запрашиваем страницу {currentPage + 1} из {totalPages} (URL: {url})");

            HttpResponseMessage response;
            string responseBody;

            try
            {
                response = client.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();
                responseBody = response.Content.ReadAsStringAsync().Result;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Ошибка при запросе страницы {currentPage}: {e.Message}");
                break;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Произошла общая ошибка: {e.Message}");
                break;
            }


            JObject jObject;
            try
            {
                jObject = JObject.Parse(responseBody);
            }
            catch (Newtonsoft.Json.JsonReaderException e)
            {
                Console.WriteLine($"Ошибка парсинга JSON на странице {currentPage}: {e.Message}");
                Console.WriteLine($"Тело ответа: {responseBody.Substring(0, Math.Min(responseBody.Length, 500))}");
                currentPage++;
                continue;
            }


            if (currentPage == 0)
            {
                totalPages = jObject["pages"]?.Value<int>() ?? 1;
                foundVacancies = jObject["found"]?.Value<int>() ?? 0;
                Console.WriteLine($"Всего найдено вакансий (по данным API): {foundVacancies}. Всего страниц: {totalPages}");
            }

            var items = jObject["items"] as JArray;

            if (items == null || !items.Any())
            {
                Console.WriteLine($"На странице {currentPage + 1} не найдено элементов 'items' или массив пуст.");
                break;
            }

            Console.WriteLine($"Найдено {items.Count} вакансий на текущей странице.");

            foreach (var item in items)
            {
                var salaryToken = item["salary"];

                if (salaryToken != null && salaryToken.Type == JTokenType.Object)
                {
                    var salaryObj = (JObject)salaryToken;
                    string currency = salaryObj["currency"]?.Value<string>();

                    if (currency != desiredCurrency)
                    {
                        continue;
                    }

                    processedVacanciesWithSalary++;

                    decimal? from = salaryObj["from"]?.Value<decimal?>();
                    decimal? to = salaryObj["to"]?.Value<decimal?>();

                    if (from.HasValue && to.HasValue)
                    {
                        salaries.Add((from.Value + to.Value) / 2);
                    }
                    else if (from.HasValue)
                    {
                        salaries.Add(from.Value);
                    }
                    else if (to.HasValue)
                    {
                        salaries.Add(to.Value);
                    }
                }
            }
            currentPage++;

            if (currentPage < totalPages)
            {
                System.Threading.Thread.Sleep(250);
            }
        }

        Console.WriteLine("\n--- Результаты ---");
        if (salaries.Any())
        {
            var averageSalary = salaries.Average();
            Console.WriteLine($"Обработано {processedVacanciesWithSalary} вакансий с зарплатой в {desiredCurrency}");
            Console.WriteLine($"Средняя зарплата: {averageSalary:N0} {desiredCurrency}");
            Console.WriteLine($"Всего по данным API было найдено: {foundVacancies} вакансий");
        }
        else
        {
            Console.WriteLine($"Зарплата в {desiredCurrency} не указана ни в одной из обработанных вакансий");
        }

        Console.WriteLine("\nНажмите любую клавишу для выхода.");
        Console.ReadKey();
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace BookStore
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        const string PATH_CATALOG = @"data\catalog.json";
        const string PATH_ORDERS = @"data\orders.json";

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
        }

        public string GetCatalog()
        {
            return File.ReadAllText(PATH_CATALOG);
        }

        public string GetOrders()
        {
            return File.ReadAllText(PATH_ORDERS);
        }

        public void SaveCatalog(string json)
        {
            using (StreamWriter sw = File.CreateText(PATH_CATALOG))
            {
                sw.WriteAsync(json);
            }
        }

        public void SaveOrders(string json)
        {
            using (StreamWriter sw = File.CreateText(PATH_ORDERS))
            {
                sw.WriteAsync(json);
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors(builder => builder.AllowAnyOrigin());

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    //await context.Response.WriteAsync("server is running");
                    context.Response.Redirect("/index.html");
                });

                endpoints.MapGet("/catalog", async context =>
                {
                    context.Response.ContentType = "application/json";
                    string json = GetCatalog();
                    await context.Response.WriteAsync(json);
                });

                endpoints.MapPost("/confirm", async context =>
                {
                    context.Response.ContentType = "application/json";
                    var requestData = (string)context.Request.Form["data"];
                    var fromCart = JObject.Parse(requestData);
                    var books = fromCart["books"].ToObject<JObject>();
                    var catalog = JObject.Parse(GetCatalog());
                    foreach (var book in books)
                    {
                        var id = book.Key;
                        var newQty = (int)catalog["books"][id]["qty"] - (int)book.Value;
                        if (newQty < 0)
                        {
                            await context.Response.WriteAsync("{\"error\":\"Один из выбранных товаров закончился. Обновите заказ\"}");
                            return;
                        }
                        catalog["books"][id]["qty"] = newQty;
                    }
                    var orders = JObject.Parse(GetOrders())["orders"].ToObject<JObject>();
                    var rnd = new Random();
                    var orderId = rnd.Next(100000, 999999);
                    orders.Add(orderId.ToString(), fromCart);
                    var ordersUpdated = JObject.Parse("{}");
                    ordersUpdated.Add("orders", orders);
                    string ordersJson = ordersUpdated.ToString(Formatting.None);
                    string catalogJson = catalog.ToString(Formatting.None);
                    SaveCatalog(catalogJson);
                    SaveOrders(ordersJson);
                    await context.Response.WriteAsync("{\"response\":{\"orderId\":\"" + orderId + "\"}}");
                });

                endpoints.MapPost("/delete", async context =>
                {
                    context.Response.ContentType = "application/json";
                    var orderId = (string)context.Request.Form["orderId"];
                    var orders = JObject.Parse(GetOrders())["orders"].ToObject<JObject>();
                    var currentOrder = orders[orderId].ToObject<JObject>();
                    var books = currentOrder["books"].ToObject<JObject>();
                    var catalog = JObject.Parse(GetCatalog());
                    foreach (var book in books)
                    {
                        var newQty = (int)catalog["books"][book.Key]["qty"] + (int)book.Value;
                        catalog["books"][book.Key]["qty"] = newQty;
                    }
                    orders.Remove(orderId);
                    var ordersUpdated = JObject.Parse("{}");
                    ordersUpdated.Add("orders", orders);
                    string ordersJson = ordersUpdated.ToString(Formatting.None);
                    string catalogJson = catalog.ToString(Formatting.None);
                    SaveCatalog(catalogJson);
                    SaveOrders(ordersJson);
                    await context.Response.WriteAsync("{\"response\":{\"order\":" + currentOrder.ToString(Formatting.None) + "}}");
                });
            });
        }
    }
}

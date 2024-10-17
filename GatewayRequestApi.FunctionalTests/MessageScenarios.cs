﻿using EventBus.Events;
using Events.Common;
using GatewayRequestApi.Application.Commands;
using GatewayRequestApi.Application.IntegrationEvents;
using GatewayRequestApi.Queries;
using MediatR;
using Message.Infrastructure;
using Message.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Amqp.Encoding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GatewayRequestApi.FunctionalTests;

public class MessageScenarios : IClassFixture<FunctionalTestWebAppFactory>
{
    private readonly FunctionalTestWebAppFactory _factory;

    public MessageScenarios(FunctionalTestWebAppFactory factory)
    {
        _factory = factory;
    }
    
    //This was just a basic test to ensure that everything was wired up. Just put a simple Get ith no params on the Controller
    //[Fact]
    //public async Task Get_message_returns_ok_status_code()
    //{
    //    var _mockMessageQueries = new Mock<IMessageQueries>();
    //    var client = _factory.CreateClient();
    //    var response = await client.GetAsync($"api/GatewayMessage");
    //    var s = await response.Content.ReadAsStringAsync();
    //    response.EnsureSuccessStatusCode();
    //}

    [Fact]
    public async Task Get_message_by_identifier_returns_ok_status_code()
    {
        var _mockMessageQueries = new Mock<IMessageQueries>();
        _mockMessageQueries.Setup(m => m.GetRsiMessageAsync(It.IsAny<string>())).Returns(Task.FromResult(new RsiMessageView()));
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IMessageQueries>(q => _mockMessageQueries.Object);
            });
        }).CreateClient();
        var identifier = "ABC123";
        var response = await client.GetAsync($"api/GatewayMessage/rsi?identifier={identifier}");
        var s = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Can_submit_rsi_success()
    {
        // Arrange
        var commandMessage = new
        {
            message = new
            {
                collectionCode = "TST",
                shelfmark = "tstMark",
                volumeNumber = "123",
                storageLocationCode = "33",
                author = "Christopher James",
                title = "A History of Yesterday",
                publicationDate = "23-04-2024",
                periodicalDate = "23-04-2024",
                articleLine1 = "hello",
                articleLine2 = "buddy",
                catalogueRecordUrl = "http://some/catalog/url",
                furtherDetailsUrl = "http://further/deets",
                dtRequired = "23-04-2024",
                route = "homeward bound",
                readingRoomStaffArea = "true",
                seatNumber = "15",
                readingCategory = "fiction",
                identifier = "GHJ456",
                readerName = "Herod Antipas",
                readerType = "1",
                operatorInformation = "Have a word",
                itemIdentity = "The life and times of a silly boy"
            }
        };
        var serialisedMsg = JsonSerializer.Serialize(commandMessage);
        var content = new StringContent(serialisedMsg, UTF8Encoding.UTF8, "application/json");

        var _mockMsgIntegrationEventService = new Mock<IMessageIntegrationEventService>();
        _mockMsgIntegrationEventService.Setup(m => m.AddAndSaveEventAsync(It.IsAny<IntegrationEvent>())).Returns(Task.FromResult(1));

        var _mockMsgRepo = new Mock<IMessageRepository>();
        _mockMsgRepo.Setup(mk => mk.UnitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(1));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<MessageContext>));
                services.AddDbContext<MessageContext>(options => options.UseSqlServer(_factory.DbConnectionString));

                services.AddScoped<IMessageRepository>(q => _mockMsgRepo.Object);
                services.AddTransient<IMessageIntegrationEventService>(i => _mockMsgIntegrationEventService.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsync("api/GatewayMessage/rsi", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

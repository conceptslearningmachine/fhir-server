﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Health.Fhir.Api.Features.ApiNotifications;
using Microsoft.Health.Fhir.Core.Features.Context;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Shared.Api.UnitTests.Features.Notifications
{
    public class ApiNotificationMiddlewareTests
    {
        private readonly IFhirRequestContextAccessor _fhirRequestContextAccessor = Substitute.For<IFhirRequestContextAccessor>();
        private readonly IFhirRequestContext _fhirRequestContext = Substitute.For<IFhirRequestContext>();
        private readonly IMediator _mediator = Substitute.For<IMediator>();

        private readonly ApiNotificationMiddleware _apiNotificationMiddleware;
        private readonly HttpContext _httpContext = new DefaultHttpContext();

        public ApiNotificationMiddlewareTests()
        {
            _fhirRequestContextAccessor.FhirRequestContext.Returns(_fhirRequestContext);

            _apiNotificationMiddleware = new ApiNotificationMiddleware(
                    httpContext => Task.CompletedTask,
                    _fhirRequestContextAccessor,
                    _mediator,
                    NullLogger<ApiNotificationMiddleware>.Instance);
        }

        [Fact]
        public async Task GivenRequestPath_WhenInvoked_DoesNotLogForHealthCheck()
        {
            _httpContext.Request.Path = FhirServerApplicationBuilderExtensions.HealthCheckPath;

            await _apiNotificationMiddleware.Invoke(_httpContext);

            await _mediator.DidNotReceiveWithAnyArgs().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GivenRequestPathButNoStorageRequestMetrics_WhenInvoked_EmitsMediatRApiEvents()
        {
            _httpContext.Request.Path = "/Observations";
            await _apiNotificationMiddleware.Invoke(_httpContext);

            await _mediator.ReceivedWithAnyArgs(1).Publish<ApiResponseNotification>(Arg.Any<ApiResponseNotification>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GivenRequestPathAndStorageRequestMetrics_WhenInvoked_EmitsMediatRApiAndStorageEvents()
        {
            _httpContext.Request.Path = "/Observation";
            await _apiNotificationMiddleware.Invoke(_httpContext);

            await _mediator.ReceivedWithAnyArgs(1).Publish<ApiResponseNotification>(Arg.Any<ApiResponseNotification>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GivenRequestPath_AndNullFhirRequestContext_WhenInvoked_DoesNotFail_AndDoesNotEmitMediatREvents()
        {
            _fhirRequestContextAccessor.FhirRequestContext.Returns((IFhirRequestContext)null);

            _httpContext.Request.Path = "/Observation";
            await _apiNotificationMiddleware.Invoke(_httpContext);

            await _mediator.DidNotReceiveWithAnyArgs().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GivenRequestPath_WhenMediatRFails_NoExceptionIsThrown()
        {
            await Task.CompletedTask;
            _mediator.WhenForAnyArgs(async x => await x.Publish(Arg.Any<ApiResponseNotification>(), Arg.Any<CancellationToken>())).Throw(new System.Exception("Failure"));

            _httpContext.Request.Path = "/Observation";
            await _apiNotificationMiddleware.Invoke(_httpContext);

            await _mediator.DidNotReceiveWithAnyArgs().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
        }
    }
}

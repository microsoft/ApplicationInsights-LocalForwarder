from django.http import HttpResponse
from django.shortcuts import render

from opencensus.trace import config_integration
from opencensus.trace.exporters.ocagent import trace_exporter
from opencensus.trace import tracer as tracer_module
from opencensus.trace.propagation.trace_context_http_header_format import TraceContextPropagator
from opencensus.trace.exporters.transports.background_thread \
    import BackgroundThreadTransport

import time
import os
import requests


INTEGRATIONS = ['httplib']

service_name = os.getenv('SERVICE_NAME', 'python-service')
config_integration.trace_integrations(INTEGRATIONS, tracer=tracer_module.Tracer(
    exporter=trace_exporter.TraceExporter(
        service_name=service_name,
        endpoint=os.getenv('OCAGENT_TRACE_EXPORTER_ENDPOINT'),
        transport=BackgroundThreadTransport),
    propagator=TraceContextPropagator()))


def call(request):
    requests.get("http://go-app:50030/call")

    return HttpResponse("hello world from " + service_name)

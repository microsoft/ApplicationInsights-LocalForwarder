const tracing = require('@opencensus/nodejs');
const ocagent = require('@opencensus/exporter-ocagent');
const propagation = require('@opencensus/propagation-tracecontext');
const url = require('url');

const LISTEN_PORT = 8008;

// Get the URL for the local forwarder from environment variable.
var endpoint = url.parse(process.env['OCAGENT_TRACE_EXPORTER_ENDPOINT']);
// Setup tracing parameters for the OC Agent.
var traceParams = {
    serviceName: process.env['SERVICE_NAME'] || 'node-service',
    host: endpoint.hostname,
    port: parseInt(endpoint.port),
};
const exporter = new ocagent.OCAgentExporter(traceParams);
delete endpoint;

// Setup the tracing with context for W3C trace propagation.
const traceContext = new propagation.TraceContextFormat();
tracing.start({
    exporter: exporter,
    samplingRate: 1.0,
    logLevel: 4,
    propagation: traceContext,
});

console.log("Starting.");

// Don't include http until after all the opencensus stuff.
const http = require('http');
// Create demo "hello world" style HTTP server.
http.createServer((req, res) => {
    var msg = "Hello Earth! " + new Date().toISOString();
    res.end(msg);
    console.log(msg + " " + req.url);
}).listen(LISTEN_PORT);

console.log("Started.");

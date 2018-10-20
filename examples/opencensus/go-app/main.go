package main

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"log"
	"net/http"
	"os"

	ocagent "contrib.go.opencensus.io/exporter/ocagent"
	"go.opencensus.io/plugin/ochttp"
	"go.opencensus.io/plugin/ochttp/propagation/tracecontext"
	"go.opencensus.io/trace"
)

func main() {
	// Register trace exporters to export the collected data.
	serviceName := os.Getenv("SERVICE_NAME")
	if len(serviceName) == 0 {
		serviceName = "go-app"
	}
	agentEndpoint := os.Getenv("OCAGENT_TRACE_EXPORTER_ENDPOINT")
	if len(agentEndpoint) == 0 {
		agentEndpoint = fmt.Sprintf("%s:%d", ocagent.DefaultAgentHost, ocagent.DefaultAgentPort)
	}

	exporter, err := ocagent.NewExporter(ocagent.WithInsecure(), ocagent.WithServiceName(serviceName), ocagent.WithAddress(agentEndpoint))
	if err != nil {
		log.Fatalf("Failed to create the agent exporter: %v", err)
	}

	trace.RegisterExporter(exporter)

	// Since we're sending data to Local Forwarder, we will always sample in order for Live Metrics Stream to work properly
	// If your application is sending high volumes of data and you're not using Live Metrics Stream, provide more conservative value here
	// Local Forwarder https://docs.microsoft.com/en-us/azure/application-insights/opencensus-local-forwarder
	// Live Metrics Stream https://docs.microsoft.com/en-us/azure/application-insights/app-insights-live-stream
	trace.ApplyConfig(trace.Config{DefaultSampler: trace.AlwaysSample()})

	client := &http.Client{Transport: &ochttp.Transport{Propagation: &tracecontext.HTTPFormat{}}}

	http.HandleFunc("/call", func(w http.ResponseWriter, req *http.Request) {

		var jsonStr = []byte(`[ { "url": "http://blank.org", "arguments": [] } ]`)
		newReq, _ := http.NewRequest("POST", "http://aspnetcore-app/api/forward", bytes.NewBuffer(jsonStr))
		newReq.Header.Set("Content-Type", "application/json")

		// Propagate the trace header info in the outgoing requests.
		newReq = newReq.WithContext(req.Context())
		msg := "Hello world from " + serviceName
		resp, err := client.Do(newReq)
		if err == nil {
			blob, _ := ioutil.ReadAll(resp.Body)
			resp.Body.Close()
			msg = fmt.Sprintf("%s\n%s", msg, blob)
		} else {
			msg = fmt.Sprintf("%s Error: %v", msg, err)
		}

		fmt.Fprintf(w, msg)
	})
	log.Fatal(http.ListenAndServe(":50030", &ochttp.Handler{Propagation: &tracecontext.HTTPFormat{}}))
}

// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

package common

import (
	"time"
	"golang.org/x/net/context"
	"github.com/sirupsen/logrus"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/exporters/zipkin"
	//"go.opentelemetry.io/otel/exporters/stdout/stdouttrace"
	"go.opentelemetry.io/otel/sdk/resource"
	"go.opentelemetry.io/contrib/propagators/b3"
	"go.opentelemetry.io/otel/propagation"	

	sdkTrace "go.opentelemetry.io/otel/sdk/trace"
	semanticConventions "go.opentelemetry.io/otel/semconv/v1.12.0"
)

func NewTracerProvider(serviceName string, environment string, id int64) (*sdkTrace.TracerProvider, error) {
	
	zipkinEndpoint := GetEnv("OTEL_EXPORTER_ZIPKIN_ENDPOINT", "http://localhost:9411/api/v2/spans")
	exporter, err := zipkin.New(zipkinEndpoint)
	if err != nil {
		return nil, err
	}

	/*exporter, err := stdouttrace.New(stdouttrace.WithPrettyPrint())
	if err != nil {
		return nil, err
	}*/

	res := resource.NewWithAttributes(
		semanticConventions.SchemaURL,
		semanticConventions.ServiceNameKey.String(serviceName),
		attribute.String("environment", environment),
		attribute.Int64("ID", id),
	)

	return sdkTrace.NewTracerProvider(
		sdkTrace.WithBatcher(exporter, sdkTrace.WithBatchTimeout(time.Second*1)),
		sdkTrace.WithResource(res),
		sdkTrace.WithSampler(sdkTrace.AlwaysSample()),
	), nil
}

func InitTracing(ctx context.Context, serviceName string, environment string, id int64) func() {
	// Set Tracer Provider
	tracerProvider, err := NewTracerProvider(serviceName, environment, id)
	if err != nil {
		logrus.Fatalf("Failed to create tracer provider: %v", err)
	}

	otel.SetTracerProvider(tracerProvider)
	otel.SetTextMapPropagator(
		propagation.NewCompositeTextMapPropagator(
			b3.New(),
			propagation.TraceContext{},
			propagation.Baggage{},
		),
	)

	logrus.Infof("set tracer provider: (name: %s environment: %s id: %d)", serviceName, environment, id)

	return func() {
		if err := tracerProvider.Shutdown(ctx); err != nil {
			logrus.Fatal(err)
		}
	}
}
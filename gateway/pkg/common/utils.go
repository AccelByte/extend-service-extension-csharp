// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

package common

import (
	"github.com/sirupsen/logrus"
	"os"
	"strconv"
	"strings"
)

func GetEnv(key, fallback string) string {
	if value, ok := os.LookupEnv(key); ok {
		return value
	}

	return fallback
}

func GetEnvInt(key string, fallback int) int {
	str := GetEnv(key, strconv.Itoa(fallback))
	val, err := strconv.Atoi(str)
	if err != nil {
		return fallback
	}

	return val
}

func GetBasePath() string {
	basePath := os.Getenv("BASE_PATH")
	if basePath == "" {
		logrus.Fatalf("BASE_PATH envar is not set or empty")
	}
	if !strings.HasPrefix(basePath, "/") {
		logrus.Fatalf("BASE_PATH envar is invalid, no leading '/' found. Valid example: /basePath")
	}

	return basePath
}

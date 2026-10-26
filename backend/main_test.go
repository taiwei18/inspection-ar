package main

import (
	"strings"
	"testing"
)

func TestLanURLs(t *testing.T) {
	if urls := lanURLs("127.0.0.1:8080"); urls != nil {
		t.Errorf("只监听本机回环地址时不应列出局域网地址, got %v", urls)
	}
	for _, u := range lanURLs(":8080") {
		if !strings.HasPrefix(u, "http://") || !strings.Contains(u, ":8080  (") {
			t.Errorf("地址格式不正确: %q", u)
		}
		t.Log(u)
	}
}

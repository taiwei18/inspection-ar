package api

import (
	"encoding/json"
	"io"
	"log"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"

	"inspection/backend/internal/issue"
)

func newTestServer(t *testing.T) *httptest.Server {
	t.Helper()
	store, err := issue.OpenStore(filepath.Join(t.TempDir(), "issues.json"))
	if err != nil {
		t.Fatal(err)
	}
	srv := httptest.NewServer(NewHandler(store, "*", log.New(io.Discard, "", 0)))
	t.Cleanup(srv.Close)
	return srv
}

func doJSON(t *testing.T, method, url, body string) (*http.Response, map[string]any) {
	t.Helper()
	req, err := http.NewRequest(method, url, strings.NewReader(body))
	if err != nil {
		t.Fatal(err)
	}
	req.Header.Set("Content-Type", "application/json")
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	var decoded map[string]any
	_ = json.NewDecoder(resp.Body).Decode(&decoded)
	return resp, decoded
}

const validBody = `{"title":"入口墙面破损","description":"左侧墙体存在裂缝","priority":"high","position":{"x":0.42,"y":0.03,"z":1.26}}`

func TestHealth(t *testing.T) {
	srv := newTestServer(t)
	resp, body := doJSON(t, http.MethodGet, srv.URL+"/api/health", "")
	if resp.StatusCode != http.StatusOK || body["status"] != "ok" {
		t.Fatalf("health 异常: %d %v", resp.StatusCode, body)
	}
}

func TestCreateListAndUpdateStatus(t *testing.T) {
	srv := newTestServer(t)

	resp, created := doJSON(t, http.MethodPost, srv.URL+"/api/issues", validBody)
	if resp.StatusCode != http.StatusCreated {
		t.Fatalf("创建应返回 201, got %d %v", resp.StatusCode, created)
	}
	id, _ := created["id"].(string)
	if id == "" || created["status"] != "open" || created["createdAt"] == nil || created["updatedAt"] == nil {
		t.Fatalf("服务端生成字段缺失: %v", created)
	}

	_, list := doJSON(t, http.MethodGet, srv.URL+"/api/issues", "")
	items, _ := list["items"].([]any)
	if len(items) != 1 {
		t.Fatalf("列表应包含 1 条, got %v", list)
	}

	resp, updated := doJSON(t, http.MethodPatch, srv.URL+"/api/issues/"+id, `{"status":"resolved"}`)
	if resp.StatusCode != http.StatusOK || updated["status"] != "resolved" {
		t.Fatalf("修改状态失败: %d %v", resp.StatusCode, updated)
	}

	resp, got := doJSON(t, http.MethodGet, srv.URL+"/api/issues/"+id, "")
	if resp.StatusCode != http.StatusOK || got["status"] != "resolved" {
		t.Fatalf("查询单条失败: %d %v", resp.StatusCode, got)
	}
}

func TestEmptyListIsArrayNotNull(t *testing.T) {
	srv := newTestServer(t)
	resp, err := http.Get(srv.URL + "/api/issues")
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	raw, _ := io.ReadAll(resp.Body)
	if strings.TrimSpace(string(raw)) != `{"items":[]}` {
		t.Fatalf("空列表应返回 [], got %s", raw)
	}
}

func TestCreateValidationErrors(t *testing.T) {
	srv := newTestServer(t)
	cases := []struct {
		name, body, code, field string
	}{
		{"空标题", `{"title":"  ","priority":"low","position":{"x":0,"y":0,"z":0}}`, "invalid_argument", "title"},
		{"优先级枚举错误", `{"title":"t","priority":"urgent","position":{"x":0,"y":0,"z":0}}`, "invalid_argument", "priority"},
		{"优先级类型错误", `{"title":"t","priority":3,"position":{"x":0,"y":0,"z":0}}`, "invalid_argument", "priority"},
		{"缺少坐标", `{"title":"t","priority":"low"}`, "invalid_argument", "position"},
		{"非法 JSON", `{"title":`, "invalid_json", ""},
		// "入口" 的 GBK 编码字节，模拟 Windows 下未按 UTF-8 发送的请求
		{"非 UTF-8 编码", "{\"title\":\"\xc8\xeb\xbf\xda\",\"priority\":\"low\",\"position\":{\"x\":0,\"y\":0,\"z\":0}}", "invalid_argument", "title"},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			resp, body := doJSON(t, http.MethodPost, srv.URL+"/api/issues", tc.body)
			errObj, _ := body["error"].(map[string]any)
			if resp.StatusCode != http.StatusBadRequest || errObj["code"] != tc.code {
				t.Fatalf("期望 400 %s, got %d %v", tc.code, resp.StatusCode, body)
			}
			if tc.field != "" && errObj["field"] != tc.field {
				t.Errorf("期望 field=%s, got %v", tc.field, errObj["field"])
			}
			if msg, _ := errObj["message"].(string); msg == "" {
				t.Error("错误信息不能为空")
			}
		})
	}
}

func TestUpdateStatusErrors(t *testing.T) {
	srv := newTestServer(t)
	_, created := doJSON(t, http.MethodPost, srv.URL+"/api/issues", validBody)
	id := created["id"].(string)

	resp, body := doJSON(t, http.MethodPatch, srv.URL+"/api/issues/"+id, `{"status":"done"}`)
	if resp.StatusCode != http.StatusBadRequest || body["error"].(map[string]any)["field"] != "status" {
		t.Errorf("非法状态应返回 400: %d %v", resp.StatusCode, body)
	}

	resp, _ = doJSON(t, http.MethodPatch, srv.URL+"/api/issues/not-exist", `{"status":"resolved"}`)
	if resp.StatusCode != http.StatusNotFound {
		t.Errorf("不存在的 ID 应返回 404, got %d", resp.StatusCode)
	}
}

func TestCORSPreflight(t *testing.T) {
	srv := newTestServer(t)
	req, _ := http.NewRequest(http.MethodOptions, srv.URL+"/api/issues/abc", nil)
	req.Header.Set("Origin", "http://localhost:5173")
	req.Header.Set("Access-Control-Request-Method", "PATCH")
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatal(err)
	}
	resp.Body.Close()
	if resp.StatusCode != http.StatusNoContent ||
		resp.Header.Get("Access-Control-Allow-Origin") != "*" ||
		!strings.Contains(resp.Header.Get("Access-Control-Allow-Methods"), "PATCH") {
		t.Fatalf("预检请求响应不正确: %d %v", resp.StatusCode, resp.Header)
	}
}

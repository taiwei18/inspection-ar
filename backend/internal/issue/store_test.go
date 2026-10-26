package issue

import (
	"errors"
	"os"
	"path/filepath"
	"testing"
)

func validInput() CreateInput {
	return CreateInput{
		Title:       "  入口墙面破损 ",
		Description: "左侧墙体存在裂缝",
		Priority:    PriorityHigh,
		Position:    &Position{X: 0.41999998688697815, Y: 0.03, Z: 1.26},
	}
}

func TestCreatePersistsAcrossReopen(t *testing.T) {
	path := filepath.Join(t.TempDir(), "issues.json")
	store, err := OpenStore(path)
	if err != nil {
		t.Fatal(err)
	}

	created, err := store.Create(validInput())
	if err != nil {
		t.Fatal(err)
	}
	if created.ID == "" || created.Status != StatusOpen || created.CreatedAt.IsZero() || !created.CreatedAt.Equal(created.UpdatedAt) {
		t.Fatalf("服务端生成的字段不正确: %+v", created)
	}
	if created.Title != "入口墙面破损" {
		t.Errorf("标题应去除首尾空白, got %q", created.Title)
	}
	if created.Position.X != 0.42 {
		t.Errorf("坐标应保留到毫米, got %v", created.Position.X)
	}

	// 模拟服务重启：重新打开同一个文件。
	reopened, err := OpenStore(path)
	if err != nil {
		t.Fatal(err)
	}
	got, err := reopened.Get(created.ID)
	if err != nil {
		t.Fatalf("重启后找不到问题: %v", err)
	}
	if got.Title != created.Title || got.Priority != created.Priority || !got.CreatedAt.Equal(created.CreatedAt) {
		t.Errorf("重启后数据不一致: %+v vs %+v", got, created)
	}
}

func TestCreateGeneratesUniqueIDsAndListsNewestFirst(t *testing.T) {
	store, _ := OpenStore(filepath.Join(t.TempDir(), "issues.json"))
	first, _ := store.Create(validInput())
	second, _ := store.Create(validInput())
	if first.ID == second.ID {
		t.Fatal("ID 重复")
	}
	list := store.List()
	if len(list) != 2 || list[0].ID != second.ID {
		t.Fatalf("列表应按创建时间倒序: %+v", list)
	}
}

func TestCreateValidation(t *testing.T) {
	cases := map[string]struct {
		mutate func(*CreateInput)
		field  string
	}{
		"空标题":     {func(in *CreateInput) { in.Title = "" }, "title"},
		"只有空格的标题": {func(in *CreateInput) { in.Title = "   " }, "title"},
		"优先级为空":   {func(in *CreateInput) { in.Priority = "" }, "priority"},
		"优先级枚举错误": {func(in *CreateInput) { in.Priority = "urgent" }, "priority"},
		"缺少坐标":    {func(in *CreateInput) { in.Position = nil }, "position"},
		"描述过长": {func(in *CreateInput) {
			b := make([]rune, MaxDescriptionLen+1)
			for i := range b {
				b[i] = '字'
			}
			in.Description = string(b)
		}, "description"},
	}
	for name, tc := range cases {
		t.Run(name, func(t *testing.T) {
			path := filepath.Join(t.TempDir(), "issues.json")
			store, _ := OpenStore(path)
			in := validInput()
			tc.mutate(&in)

			_, err := store.Create(in)
			var ve *ValidationError
			if !errors.As(err, &ve) || ve.Field != tc.field {
				t.Fatalf("期望字段 %s 的校验错误, got %v", tc.field, err)
			}
			if _, statErr := os.Stat(path); !errors.Is(statErr, os.ErrNotExist) {
				t.Error("校验失败时不应写入数据文件")
			}
		})
	}
}

func TestUpdateStatus(t *testing.T) {
	path := filepath.Join(t.TempDir(), "issues.json")
	store, _ := OpenStore(path)
	created, _ := store.Create(validInput())

	updated, err := store.UpdateStatus(created.ID, StatusInProgress)
	if err != nil {
		t.Fatal(err)
	}
	if updated.Status != StatusInProgress || updated.UpdatedAt.Before(created.UpdatedAt) {
		t.Fatalf("状态未更新: %+v", updated)
	}

	reopened, _ := OpenStore(path)
	if got, _ := reopened.Get(created.ID); got.Status != StatusInProgress {
		t.Errorf("重启后状态丢失: %s", got.Status)
	}

	var ve *ValidationError
	if _, err := store.UpdateStatus(created.ID, "done"); !errors.As(err, &ve) || ve.Field != "status" {
		t.Errorf("非法状态应返回校验错误, got %v", err)
	}
	if _, err := store.UpdateStatus("missing-id", StatusResolved); !errors.Is(err, ErrNotFound) {
		t.Errorf("不存在的 ID 应返回 ErrNotFound, got %v", err)
	}
}

func TestOpenStoreMissingFileStartsEmpty(t *testing.T) {
	store, err := OpenStore(filepath.Join(t.TempDir(), "nested", "issues.json"))
	if err != nil {
		t.Fatal(err)
	}
	if store.Count() != 0 {
		t.Fatal("新文件应为空列表")
	}
	// 目录不存在时，首次写入应自动创建。
	if _, err := store.Create(validInput()); err != nil {
		t.Fatal(err)
	}
}

func TestOpenStoreRejectsCorruptFile(t *testing.T) {
	path := filepath.Join(t.TempDir(), "issues.json")
	if err := os.WriteFile(path, []byte("{not json"), 0o644); err != nil {
		t.Fatal(err)
	}
	if _, err := OpenStore(path); err == nil {
		t.Fatal("损坏的数据文件应返回错误")
	}
}

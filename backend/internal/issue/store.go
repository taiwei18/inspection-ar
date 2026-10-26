package issue

import (
	"bytes"
	"crypto/rand"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"slices"
	"sync"
	"time"
)

var ErrNotFound = errors.New("issue not found")

// Store 把全部问题保存在内存中，每次写操作都把完整列表写回 JSON 文件。
// 数据量是巡检原型级别（几百到几千条），整文件重写足够简单可靠。
type Store struct {
	mu     sync.RWMutex
	path   string
	issues []Issue // 按创建顺序排列

	now   func() time.Time
	newID func() (string, error)
}

// OpenStore 从 path 加载已有数据；文件不存在时从空列表开始。
// 文件损坏时返回错误而不是当作空数据，避免下一次写入把旧数据覆盖掉。
func OpenStore(path string) (*Store, error) {
	s := &Store{
		path:   path,
		issues: []Issue{},
		now:    func() time.Time { return time.Now().UTC().Truncate(time.Millisecond) },
		newID:  newUUID,
	}

	data, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return s, nil
	}
	if err != nil {
		return nil, fmt.Errorf("读取数据文件 %s 失败: %w", path, err)
	}
	if len(bytes.TrimSpace(data)) == 0 {
		return s, nil
	}
	if err := json.Unmarshal(data, &s.issues); err != nil {
		return nil, fmt.Errorf("数据文件 %s 格式错误，为避免覆盖已拒绝启动: %w", path, err)
	}
	if s.issues == nil {
		s.issues = []Issue{}
	}
	return s, nil
}

func (s *Store) Path() string { return s.path }

func (s *Store) Count() int {
	s.mu.RLock()
	defer s.mu.RUnlock()
	return len(s.issues)
}

// List 返回全部问题的副本，最新创建的排在最前面。
func (s *Store) List() []Issue {
	s.mu.RLock()
	defer s.mu.RUnlock()
	out := slices.Clone(s.issues)
	slices.Reverse(out)
	return out
}

func (s *Store) Get(id string) (Issue, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	if i := s.indexOf(id); i >= 0 {
		return s.issues[i], nil
	}
	return Issue{}, ErrNotFound
}

// Create 校验输入，生成 ID 与时间戳，状态固定为 open。
func (s *Store) Create(in CreateInput) (Issue, error) {
	if err := in.Normalize(); err != nil {
		return Issue{}, err
	}
	id, err := s.newID()
	if err != nil {
		return Issue{}, fmt.Errorf("生成 ID 失败: %w", err)
	}

	s.mu.Lock()
	defer s.mu.Unlock()

	now := s.now()
	created := Issue{
		ID:          id,
		Title:       in.Title,
		Description: in.Description,
		Priority:    in.Priority,
		Status:      StatusOpen,
		Position:    *in.Position,
		CreatedAt:   now,
		UpdatedAt:   now,
	}
	// 先写文件，成功后再替换内存数据：写盘失败时内存和磁盘仍然一致。
	next := append(slices.Clone(s.issues), created)
	if err := s.save(next); err != nil {
		return Issue{}, err
	}
	s.issues = next
	return created, nil
}

func (s *Store) UpdateStatus(id string, status Status) (Issue, error) {
	if !status.Valid() {
		return Issue{}, &ValidationError{"status", "status 必须是 open、in_progress、resolved 之一"}
	}

	s.mu.Lock()
	defer s.mu.Unlock()

	i := s.indexOf(id)
	if i < 0 {
		return Issue{}, ErrNotFound
	}
	next := slices.Clone(s.issues)
	next[i].Status = status
	next[i].UpdatedAt = s.now()
	if err := s.save(next); err != nil {
		return Issue{}, err
	}
	s.issues = next
	return next[i], nil
}

func (s *Store) indexOf(id string) int {
	return slices.IndexFunc(s.issues, func(it Issue) bool { return it.ID == id })
}

// save 先写临时文件再重命名，进程中途崩溃也不会留下写了一半的数据文件。
func (s *Store) save(issues []Issue) error {
	data, err := json.MarshalIndent(issues, "", "  ")
	if err != nil {
		return err
	}
	if err := os.MkdirAll(filepath.Dir(s.path), 0o755); err != nil {
		return fmt.Errorf("创建数据目录失败: %w", err)
	}
	tmp := s.path + ".tmp"
	if err := os.WriteFile(tmp, data, 0o644); err != nil {
		return fmt.Errorf("写入数据文件失败: %w", err)
	}
	if err := os.Rename(tmp, s.path); err != nil {
		return fmt.Errorf("替换数据文件失败: %w", err)
	}
	return nil
}

// newUUID 生成随机 UUID v4，碰撞概率可以忽略。
func newUUID() (string, error) {
	var b [16]byte
	if _, err := rand.Read(b[:]); err != nil {
		return "", err
	}
	b[6] = (b[6] & 0x0f) | 0x40 // version 4
	b[8] = (b[8] & 0x3f) | 0x80 // RFC 4122 variant
	return fmt.Sprintf("%x-%x-%x-%x-%x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:16]), nil
}

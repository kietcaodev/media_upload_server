import React, { useState, useEffect } from 'react';
import { Layout, Menu, ConfigProvider, theme, Button, Input, message, Card, Typography, Form, Alert } from 'antd';
import {
  DashboardOutlined, UploadOutlined, UnorderedListOutlined,
  SettingOutlined, UserOutlined, LockOutlined, LogoutOutlined
} from '@ant-design/icons';
import DashboardPage from './pages/DashboardPage';
import UploadPage from './pages/UploadPage';
import JobsPage from './pages/JobsPage';
import ConfigPage from './pages/ConfigPage';
import { useSettingsStore } from './stores/settingsStore';
import { dashboardApi } from './api/services';

const { Header, Sider, Content } = Layout;
const { Title, Text } = Typography;
type Page = 'dashboard' | 'upload' | 'jobs' | 'config';

// Đăng nhập bằng username/password (Basic Auth) cho người dùng qua giao diện
// web. Các phương thức Bearer Token/API Key chỉ dành cho hệ thống ngoài gọi
// API upload trực tiếp – được quản lý ở trang Cấu hình → Credentials, không
// hiển thị ở màn hình đăng nhập này.
function LoginPage({ onAuth }: { onAuth: () => void }) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleLogin = async (values: { username: string; password: string }) => {
    setError('');
    setLoading(true);
    localStorage.setItem('auth_token', btoa(`${values.username}:${values.password}`));
    localStorage.setItem('auth_type', 'Basic');
    try {
      // Gọi thử 1 API cần xác thực để xác minh username/password đúng ngay
      // tại lúc đăng nhập, thay vì để tới lần gọi API đầu tiên mới báo lỗi.
      await dashboardApi.stats();
      message.success('Đăng nhập thành công');
      onAuth();
    } catch {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth_type');
      setError('Sai tên đăng nhập hoặc mật khẩu');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      minHeight: '100vh', width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'linear-gradient(135deg, #1677ff 0%, #722ed1 100%)', padding: 16,
    }}>
      <Card style={{ width: 380, maxWidth: '100%', borderRadius: 12, boxShadow: '0 8px 32px rgba(0,0,0,.25)' }}>
        <div style={{ textAlign: 'center', marginBottom: 24 }}>
          <div style={{ fontSize: 40, lineHeight: 1 }}>📹</div>
          <Title level={3} style={{ margin: '12px 0 4px' }}>Media Upload</Title>
          <Text type="secondary">Đăng nhập để tiếp tục</Text>
        </div>
        {error && <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} />}
        <Form layout="vertical" onFinish={handleLogin} disabled={loading} requiredMark={false}>
          <Form.Item name="username" rules={[{ required: true, message: 'Nhập tên đăng nhập' }]}>
            <Input prefix={<UserOutlined />} placeholder="Tên đăng nhập" size="large" autoFocus />
          </Form.Item>
          <Form.Item name="password" rules={[{ required: true, message: 'Nhập mật khẩu' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="Mật khẩu" size="large" />
          </Form.Item>
          <Form.Item style={{ marginBottom: 0 }}>
            <Button type="primary" htmlType="submit" size="large" block loading={loading}>Đăng nhập</Button>
          </Form.Item>
        </Form>
      </Card>
    </div>
  );
}

export default function App() {
  const [page, setPage] = useState<Page>('dashboard');
  const [authed, setAuthed] = useState(!!localStorage.getItem('auth_token'));
  const { fetch: fetchSettings } = useSettingsStore();

  useEffect(() => { if (authed) fetchSettings(); }, [authed]);

  if (!authed) return (
    <ConfigProvider theme={{ algorithm: theme.defaultAlgorithm }}>
      <LoginPage onAuth={() => setAuthed(true)} />
    </ConfigProvider>
  );

  const pages: Record<Page, React.ReactNode> = {
    dashboard: <DashboardPage />, upload: <UploadPage />,
    jobs: <JobsPage />, config: <ConfigPage />,
  };

  return (
    <ConfigProvider theme={{ algorithm: theme.defaultAlgorithm }}>
      <Layout style={{ minHeight: '100vh' }}>
        <Sider collapsible breakpoint="lg" collapsedWidth={64}>
          <div style={{ padding: '16px', color: '#fff', fontWeight: 700, fontSize: 16 }}>📹 Media Upload</div>
          <Menu theme="dark" mode="inline" selectedKeys={[page]}
            onClick={({ key }) => setPage(key as Page)}
            items={[
              { key: 'dashboard', icon: <DashboardOutlined />, label: 'Dashboard' },
              { key: 'upload', icon: <UploadOutlined />, label: 'Upload' },
              { key: 'jobs', icon: <UnorderedListOutlined />, label: 'Jobs' },
              { key: 'config', icon: <SettingOutlined />, label: 'Cấu hình' },
            ]}
          />
        </Sider>
        <Layout>
          <Header style={{ background: '#fff', padding: '0 16px', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', boxShadow: '0 1px 4px rgba(0,0,0,.12)' }}>
            <Button icon={<LogoutOutlined />} onClick={() => { localStorage.clear(); setAuthed(false); }}>Đăng xuất</Button>
          </Header>
          <Content style={{ margin: 16, padding: 24, background: '#f5f5f5', borderRadius: 8, overflow: 'auto' }}>
            {pages[page]}
          </Content>
        </Layout>
      </Layout>
    </ConfigProvider>
  );
}


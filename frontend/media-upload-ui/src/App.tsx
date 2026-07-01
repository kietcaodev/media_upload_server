import React, { useState, useEffect } from 'react';
import { Layout, Menu, ConfigProvider, theme, Button, Space, Input, Modal, message } from 'antd';
import {
  DashboardOutlined, UploadOutlined, UnorderedListOutlined,
  SettingOutlined, UserOutlined, LockOutlined, LogoutOutlined
} from '@ant-design/icons';
import DashboardPage from './pages/DashboardPage';
import UploadPage from './pages/UploadPage';
import JobsPage from './pages/JobsPage';
import ConfigPage from './pages/ConfigPage';
import { useSettingsStore } from './stores/settingsStore';

const { Header, Sider, Content } = Layout;
type Page = 'dashboard' | 'upload' | 'jobs' | 'config';

// Đăng nhập bằng username/password (Basic Auth) cho người dùng qua giao diện
// web. Các phương thức Bearer Token/API Key chỉ dành cho hệ thống ngoài gọi
// API upload trực tiếp – được quản lý ở trang Cấu hình → Credentials, không
// hiển thị ở màn hình đăng nhập này.
function AuthModal({ onAuth }: { onAuth: () => void }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  const handleAuth = () => {
    if (!username || !password) { message.warning('Nhập tên đăng nhập và mật khẩu'); return; }
    localStorage.setItem('auth_token', btoa(`${username}:${password}`));
    localStorage.setItem('auth_type', 'Basic');
    message.success('Đăng nhập thành công');
    onAuth();
  };

  return (
    <Modal title="Đăng nhập" open closable={false} onOk={handleAuth} okText="Đăng nhập" cancelButtonProps={{ style: { display: 'none' } }}>
      <Space direction="vertical" style={{ width: '100%' }}>
        <Input prefix={<UserOutlined />} placeholder="Tên đăng nhập" value={username}
          onChange={e => setUsername(e.target.value)} onPressEnter={handleAuth} autoFocus />
        <Input.Password prefix={<LockOutlined />} placeholder="Mật khẩu" value={password}
          onChange={e => setPassword(e.target.value)} onPressEnter={handleAuth} />
      </Space>
    </Modal>
  );
}

export default function App() {
  const [page, setPage] = useState<Page>('dashboard');
  const [authed, setAuthed] = useState(!!localStorage.getItem('auth_token'));
  const { fetch: fetchSettings } = useSettingsStore();

  useEffect(() => { if (authed) fetchSettings(); }, [authed]);

  if (!authed) return (
    <ConfigProvider theme={{ algorithm: theme.defaultAlgorithm }}>
      <AuthModal onAuth={() => setAuthed(true)} />
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


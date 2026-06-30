import React, { useState, useEffect } from 'react';
import { Layout, Menu, ConfigProvider, theme, Button, Space, Input, Modal, Select, message } from 'antd';
import {
  DashboardOutlined, UploadOutlined, UnorderedListOutlined,
  SettingOutlined, LockOutlined
} from '@ant-design/icons';
import DashboardPage from './pages/DashboardPage';
import UploadPage from './pages/UploadPage';
import JobsPage from './pages/JobsPage';
import ConfigPage from './pages/ConfigPage';
import { useSettingsStore } from './stores/settingsStore';

const { Header, Sider, Content } = Layout;
type Page = 'dashboard' | 'upload' | 'jobs' | 'config';

function AuthModal({ onAuth }: { onAuth: () => void }) {
  const [authType, setAuthType] = useState<'Bearer' | 'Basic' | 'ApiKey'>('Bearer');
  const [token, setToken] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  const handleAuth = () => {
    if (!token && authType !== 'Basic') { message.warning('Nhập token'); return; }
    if (authType === 'Basic') {
      if (!username || !password) { message.warning('Nhập username/password'); return; }
      localStorage.setItem('auth_token', btoa(`${username}:${password}`));
    } else {
      localStorage.setItem('auth_token', token);
    }
    localStorage.setItem('auth_type', authType);
    message.success('Đã lưu xác thực');
    onAuth();
  };

  return (
    <Modal title="Xác thực" open closable={false} onOk={handleAuth} okText="Xác nhận" cancelButtonProps={{ style: { display: 'none' } }}>
      <Space direction="vertical" style={{ width: '100%' }}>
        <Select value={authType} onChange={v => setAuthType(v)} style={{ width: '100%' }}
          options={[{ value: 'Bearer', label: 'Bearer Token' }, { value: 'Basic', label: 'Basic Auth' }, { value: 'ApiKey', label: 'API Key' }]} />
        {authType === 'Basic' ? (
          <>
            <Input placeholder="Username" value={username} onChange={e => setUsername(e.target.value)} />
            <Input.Password placeholder="Password" value={password} onChange={e => setPassword(e.target.value)} />
          </>
        ) : (
          <Input.Password placeholder={authType === 'Bearer' ? 'Bearer Token' : 'API Key'} value={token} onChange={e => setToken(e.target.value)} />
        )}
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
            <Button icon={<LockOutlined />} onClick={() => { localStorage.clear(); setAuthed(false); }}>Đổi token</Button>
          </Header>
          <Content style={{ margin: 16, padding: 24, background: '#f5f5f5', borderRadius: 8, overflow: 'auto' }}>
            {pages[page]}
          </Content>
        </Layout>
      </Layout>
    </ConfigProvider>
  );
}


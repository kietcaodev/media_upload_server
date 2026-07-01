import { useEffect, useState } from 'react';
import { Tabs, Card, Table, Button, Modal, Form, Input, TimePicker, Switch, Space, message, Tag, Popconfirm, Typography, Select } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { timeWindowApi, erpApi, credentialApi } from '../api/services';
import { formatLocalTime } from '../api/client';
import type { TimeWindowDto, ErpEndpointDto, CredentialDto, CreateCredentialResponse } from '../types';
import { useSettingsStore } from '../stores/settingsStore';

const { Title, Text, Paragraph } = Typography;

// ─────────────────────────────── Time Windows ──────────────────────
function TimeWindowTab() {
  const [data, setData] = useState<TimeWindowDto[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<TimeWindowDto | null>(null);
  const [form] = Form.useForm();

  const load = () => timeWindowApi.list().then(setData);
  useEffect(() => { load(); }, []);

  const save = async (vals: any) => {
    const payload = {
      name: vals.name,
      startTime: vals.startTime.format('HH:mm'),
      endTime: vals.endTime.format('HH:mm'),
      daysOfWeek: (vals.daysOfWeek as number[]).join(','),
      enabled: vals.enabled ?? true,
    };
    if (editing) await timeWindowApi.update(editing.id, payload);
    else await timeWindowApi.create(payload);
    message.success('Đã lưu');
    setModalOpen(false);
    load();
  };

  const openEdit = (row?: TimeWindowDto) => {
    setEditing(row || null);
    form.setFieldsValue(row ? {
      ...row,
      startTime: dayjs(row.startTime, 'HH:mm'),
      endTime: dayjs(row.endTime, 'HH:mm'),
      daysOfWeek: row.daysOfWeek.split(',').map(Number),
    } : { enabled: true, daysOfWeek: [1, 2, 3, 4, 5] });
    setModalOpen(true);
  };

  const DOW_OPTIONS = [
    { label: 'T2', value: 1 }, { label: 'T3', value: 2 }, { label: 'T4', value: 3 },
    { label: 'T5', value: 4 }, { label: 'T6', value: 5 }, { label: 'T7', value: 6 }, { label: 'CN', value: 7 },
  ];

  return (
    <>
      <Button type="primary" icon={<PlusOutlined />} onClick={() => openEdit()} style={{ marginBottom: 16 }}>
        Thêm Time Window
      </Button>
      <Table dataSource={data} rowKey="id" size="small" pagination={false}
        columns={[
          { title: 'Tên', dataIndex: 'name' },
          { title: 'Từ', dataIndex: 'startTime', width: 80 },
          { title: 'Đến', dataIndex: 'endTime', width: 80 },
          { title: 'Ngày', dataIndex: 'daysOfWeek', render: (v: string) =>
            v.split(',').map(d => <Tag key={d}>{['', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'][+d]}</Tag>)
          },
          { title: 'Bật', dataIndex: 'enabled', render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? 'ON' : 'OFF'}</Tag>, width: 70 },
          { title: '', key: 'actions', width: 100, render: (_, row) => (
            <Space>
              <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(row)} />
              <Popconfirm title="Xóa?" onConfirm={() => timeWindowApi.remove(row.id).then(load)}>
                <Button size="small" danger icon={<DeleteOutlined />} />
              </Popconfirm>
            </Space>
          )},
        ]}
      />
      <Modal title={editing ? 'Sửa Time Window' : 'Thêm Time Window'} open={modalOpen}
        onCancel={() => setModalOpen(false)} onOk={() => form.submit()} destroyOnClose>
        <Form form={form} layout="vertical" onFinish={save}>
          <Form.Item name="name" label="Tên" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="startTime" label="Giờ bắt đầu (GMT+7)" rules={[{ required: true }]}>
            <TimePicker format="HH:mm" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="endTime" label="Giờ kết thúc (GMT+7)" rules={[{ required: true }]}>
            <TimePicker format="HH:mm" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="daysOfWeek" label="Ngày trong tuần" rules={[{ required: true }]}>
            <Select mode="multiple" options={DOW_OPTIONS} />
          </Form.Item>
          <Form.Item name="enabled" label="Bật" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ─────────────────────────────── ERP Config ────────────────────────
// Gợi ý URL sẵn có cho các công ty đã biết (mang từ server.js cũ sang) – chỉ để
// điền nhanh, người dùng vẫn có thể sửa/thêm target bất kỳ.
const ERP_URL_SUGGESTIONS: Record<string, string> = {
  DND: 'https://locnuoc365.xyz/api/order/upload-videos',
  ZOMZEM: 'https://zomzem.xyz/api/order/upload-videos',
  ZOZIN: 'https://erp.zozin.vn/api/order/upload-videos',
};

function ErpConfigTab() {
  const [data, setData] = useState<ErpEndpointDto[]>([]);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [selected, setSelected] = useState<ErpEndpointDto | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm] = Form.useForm();
  const [creating, setCreating] = useState(false);

  const load = () => erpApi.list().then(d => { setData(d); if (d.length && !selected) setSelected(d[0]); });
  useEffect(() => { load(); }, []);

  useEffect(() => {
    if (selected) form.setFieldsValue({ url: selected.url, enabled: selected.enabled, token: '' });
  }, [selected]);

  const save = async (vals: any) => {
    if (!selected) return;
    if (!vals.token) { message.warning('Nhập token mới để cập nhật'); return; }
    setSaving(true);
    try {
      await erpApi.upsert({ target: selected.target, url: vals.url, token: vals.token, enabled: vals.enabled });
      message.success('Đã lưu ERP config');
      load();
    } finally { setSaving(false); }
  };

  const createTarget = async (vals: any) => {
    const target = String(vals.target).trim().toUpperCase();
    setCreating(true);
    try {
      const created = await erpApi.upsert({ target, url: vals.url, token: vals.token, enabled: vals.enabled ?? true });
      message.success(`Đã tạo ERP target ${target}`);
      setCreateOpen(false);
      createForm.resetFields();
      await load();
      setSelected(created);
    } finally { setCreating(false); }
  };

  return (
    <div>
      <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)} style={{ marginBottom: 16 }}>
        Thêm ERP Target
      </Button>
      <div style={{ display: 'flex', gap: 16 }}>
        <Card style={{ minWidth: 180 }} size="small" title="ERP Targets">
          {data.length === 0 && <Text type="secondary">Chưa có target nào – bấm "Thêm ERP Target"</Text>}
          {data.map(e => (
            <div key={e.id} style={{ padding: '8px 0', cursor: 'pointer', fontWeight: selected?.id === e.id ? 600 : 400 }}
              onClick={() => setSelected(e)}>
              <Tag color={e.enabled ? 'green' : 'red'}>{e.target}</Tag>
            </div>
          ))}
        </Card>
        {selected && (
          <Card title={`Cấu hình ${selected.target}`} style={{ flex: 1 }} size="small">
            <Form form={form} layout="vertical" onFinish={save}>
              <Form.Item name="url" label="URL" rules={[{ required: true }]}>
                <Input />
              </Form.Item>
              <Form.Item name="token" label="Token mới (bỏ trống = giữ nguyên)" extra={`Hiện tại: ${selected.tokenPrefix}`}>
                <Input.Password placeholder="Nhập token mới để thay thế" />
              </Form.Item>
              <Form.Item name="enabled" label="Enabled" valuePropName="checked">
                <Switch />
              </Form.Item>
              <Button type="primary" htmlType="submit" loading={saving}>Lưu</Button>
            </Form>
          </Card>
        )}
      </div>

      <Modal title="Thêm ERP Target mới" open={createOpen} onCancel={() => setCreateOpen(false)}
        onOk={() => createForm.submit()} confirmLoading={creating} destroyOnClose>
        <Form form={createForm} layout="vertical" onFinish={createTarget} initialValues={{ enabled: true }}>
          <Form.Item name="target" label="Mã công ty (target)" rules={[{ required: true }]}
            extra="Viết hoa tự động, vd: DND, ZOMZEM, ZOZIN hoặc mã công ty mới">
            <Input placeholder="Vd: DND" />
          </Form.Item>
          <Space size={4} style={{ marginBottom: 12 }} wrap>
            <Text type="secondary">Gợi ý:</Text>
            {Object.entries(ERP_URL_SUGGESTIONS).map(([key, url]) => (
              <Tag key={key} style={{ cursor: 'pointer' }}
                onClick={() => createForm.setFieldsValue({ target: key, url })}>
                {key}
              </Tag>
            ))}
          </Space>
          <Form.Item name="url" label="URL" rules={[{ required: true }]}>
            <Input placeholder="https://..." />
          </Form.Item>
          <Form.Item name="token" label="Token" rules={[{ required: true }]}>
            <Input.Password placeholder="Token ERP cấp cho công ty này" />
          </Form.Item>
          <Form.Item name="enabled" label="Enabled" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}

// ─────────────────────────────── Credentials ───────────────────────
function CredentialsTab() {
  const [data, setData] = useState<CredentialDto[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [newCred, setNewCred] = useState<CreateCredentialResponse | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [form] = Form.useForm();

  const load = () => credentialApi.list().then(setData);
  useEffect(() => { load(); }, []);

  const create = async (vals: any) => {
    const result = await credentialApi.create(vals);
    setNewCred(result);
    setConfirmed(false);
    load();
  };

  return (
    <>
      <Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)} style={{ marginBottom: 16 }}>
        Tạo Credential
      </Button>
      <Table dataSource={data} rowKey="id" size="small" pagination={false}
        scroll={{ x: 800 }}
        columns={[
          { title: 'Tên', dataIndex: 'name' },
          { title: 'Loại', dataIndex: 'authType', width: 80 },
          { title: 'Prefix', dataIndex: 'tokenPrefix', width: 110 },
          { title: 'Username', dataIndex: 'username', width: 120 },
          { title: 'Permissions', key: 'perms', render: (_, r: CredentialDto) => (
            <Space size={4}>
              {r.canUpload && <Tag color="blue">upload</Tag>}
              {r.canReadJobs && <Tag color="cyan">read</Tag>}
              {r.canConfig && <Tag color="purple">config</Tag>}
            </Space>
          )},
          { title: 'Status', dataIndex: 'enabled', width: 80, render: (v) => <Tag color={v ? 'green' : 'red'}>{v ? 'ON' : 'OFF'}</Tag> },
          { title: 'Dùng lần cuối', dataIndex: 'lastUsedAtUtc', width: 160, render: (v) => formatLocalTime(v) },
          { title: '', key: 'actions', width: 120, render: (_, row) => (
            <Space size={4}>
              <Popconfirm title="Xoay token mới?" onConfirm={async () => {
                const r = await credentialApi.rotate(row.id);
                Modal.info({ title: 'Token mới', content: <Paragraph copyable>{r.rawToken}</Paragraph> });
              }}>
                <Button size="small" icon={<ReloadOutlined />}>Rotate</Button>
              </Popconfirm>
              <Popconfirm title="Xóa?" onConfirm={() => credentialApi.remove(row.id).then(load)}>
                <Button size="small" danger icon={<DeleteOutlined />} />
              </Popconfirm>
            </Space>
          )},
        ]}
      />

      {/* Create modal */}
      <Modal title="Tạo Credential mới" open={modalOpen} onCancel={() => setModalOpen(false)}
        onOk={() => form.submit()} destroyOnClose>
        <Form form={form} layout="vertical" onFinish={create}>
          <Form.Item name="name" label="Tên" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="authType" label="Loại auth" rules={[{ required: true }]}>
            <Select options={[{ value: 0, label: 'Bearer' }, { value: 1, label: 'Basic' }, { value: 2, label: 'ApiKey' }]} />
          </Form.Item>
          <Form.Item name="username" label="Username (chỉ Basic auth)"><Input /></Form.Item>
          <Form.Item name="canUpload" label="Quyền Upload" valuePropName="checked"><Switch /></Form.Item>
          <Form.Item name="canReadJobs" label="Quyền Read Jobs" valuePropName="checked"><Switch defaultChecked /></Form.Item>
          <Form.Item name="canConfig" label="Quyền Config" valuePropName="checked"><Switch /></Form.Item>
          <Form.Item name="allowedErp" label="ERP được phép (rỗng = tất cả)"><Input placeholder="DND,ZOMZEM,ZOZIN" /></Form.Item>
        </Form>
      </Modal>

      {/* Show raw token once */}
      <Modal title="⚠️ Lưu token – hiện chỉ 1 lần!" open={!!newCred}
        onOk={() => { if (confirmed) setNewCred(null); else message.warning('Xác nhận đã lưu token trước khi đóng'); }}
        onCancel={() => { if (confirmed) setNewCred(null); else message.warning('Phải xác nhận đã lưu token'); }}
        okText="Đóng" cancelButtonProps={{ style: { display: 'none' } }}>
        {newCred && (
          <Space direction="vertical" style={{ width: '100%' }}>
            <Text>Token (chỉ hiển thị 1 lần):</Text>
            <Paragraph copyable style={{ background: '#f5f5f5', padding: 8, borderRadius: 4, wordBreak: 'break-all' }}>
              {newCred.rawToken}
            </Paragraph>
            <Switch checkedChildren="Đã lưu" unCheckedChildren="Chưa lưu" checked={confirmed} onChange={setConfirmed} />
          </Space>
        )}
      </Modal>
    </>
  );
}

// ─────────────────────────────── System Settings ───────────────────
function SystemSettingsTab() {
  const { settings, saving, fetch, save, reset } = useSettingsStore();
  const [form] = Form.useForm();

  useEffect(() => { fetch(); }, []);

  useEffect(() => {
    const vals: Record<string, string> = {};
    settings.forEach(s => { vals[s.key] = s.value; });
    form.setFieldsValue(vals);
  }, [settings]);

  const categories = [
    { prefix: 'nas.', title: 'NAS / Đường dẫn' },
    { prefix: 'upload.', title: 'Upload' },
    { prefix: 'worker.', title: 'Worker' },
    { prefix: 'ratelimit.', title: 'Rate Limit' },
    { prefix: 'cors.', title: 'CORS' },
    { prefix: 'system.', title: 'Hệ thống' },
  ];

  const handleSave = async () => {
    const vals = form.getFieldsValue();
    await save(vals);
    message.success('Đã lưu cài đặt');
  };

  return (
    <Form form={form} layout="vertical">
      {categories.map(({ prefix, title }) => {
        const group = settings.filter(s => s.key.startsWith(prefix));
        if (!group.length) return null;
        return (
          <Card key={prefix} title={title} size="small" style={{ marginBottom: 16 }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 8 }}>
              {group.map(s => (
                <Form.Item key={s.key} name={s.key}
                  label={<Space size={4}><span>{s.key.split('.')[1]}</span>{s.hotReload ? <Tag color="green" style={{ fontSize: 10 }}>hot</Tag> : <Tag color="orange" style={{ fontSize: 10 }}>restart</Tag>}</Space>}
                  extra={s.description} style={{ marginBottom: 4 }}>
                  <Space.Compact style={{ width: '100%' }}>
                    <Input />
                    <Button onClick={() => reset(s.key).then(() => message.success(`Reset ${s.key}`))}>↺</Button>
                  </Space.Compact>
                </Form.Item>
              ))}
            </div>
          </Card>
        );
      })}
      <Button type="primary" loading={saving} onClick={handleSave} size="large">
        Lưu tất cả cài đặt
      </Button>
    </Form>
  );
}

// ─────────────────────────────── Main Config Page ──────────────────
export default function ConfigPage() {
  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Title level={3} style={{ margin: 0 }}>Cấu hình</Title>
      <Card>
        <Tabs
          items={[
            { key: 'timewindow', label: 'Time Windows', children: <TimeWindowTab /> },
            { key: 'erp', label: 'ERP Endpoints', children: <ErpConfigTab /> },
            { key: 'credentials', label: 'Credentials / Auth', children: <CredentialsTab /> },
            { key: 'settings', label: 'System Settings', children: <SystemSettingsTab /> },
          ]}
        />
      </Card>
    </Space>
  );
}

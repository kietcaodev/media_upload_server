import { useEffect, useState } from 'react';
import { Card, Form, Input, Select, Button, Upload, message, Space, Row, Col, Typography } from 'antd';
import { InboxOutlined } from '@ant-design/icons';
import type { UploadFile } from 'antd';
import { erpApi, uploadApi } from '../api/services';

const { Dragger } = Upload;
const { Title } = Typography;

export default function UploadPage() {
  const [form] = Form.useForm();
  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [uploading, setUploading] = useState(false);
  const [erpTargets, setErpTargets] = useState<string[]>([]);

  useEffect(() => {
    // Lấy đúng danh sách ERP đã cấu hình (tab Cấu hình > ERP Endpoints) và đang
    // bật – tránh cho user chọn 1 target chưa cấu hình URL/token (sẽ bị ERP
    // push "skipped" âm thầm dù upload/tạo job vẫn thành công).
    erpApi.list().then(list => setErpTargets(list.filter(e => e.enabled).map(e => e.target)));
  }, []);

  const handleUpload = async (values: Record<string, string>) => {
    const files = fileList
      .filter(f => f.originFileObj)
      .map(f => f.originFileObj as File);

    if (files.length === 0) {
      message.warning('Vui lòng chọn ít nhất 1 file');
      return;
    }

    setUploading(true);
    try {
      const result = await uploadApi.upload(files, values);
      message.success(`Upload thành công ${result.length} file(s). Job đã được tạo.`);
      form.resetFields();
      setFileList([]);
    } catch (err: any) {
      message.error(err?.response?.data?.error || 'Upload thất bại');
    } finally {
      setUploading(false);
    }
  };

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Title level={3} style={{ margin: 0 }}>Upload Video</Title>

      <Card>
        <Form form={form} layout="vertical" onFinish={handleUpload}>
          <Row gutter={16}>
            <Col xs={24} sm={12}>
              <Form.Item name="erpTarget" label="ERP Target" rules={[{ required: true }]}>
                <Select placeholder="Chọn ERP" options={erpTargets.map(t => ({ value: t, label: t }))} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="orderId" label="Order ID">
                <Input placeholder="Nhập order ID" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="nvktId" label="NVKT ID">
                <Input placeholder="Nhập NVKT ID" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="flowId" label="Flow ID">
                <Input placeholder="Nhập flow ID" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="longitude" label="Longitude">
                <Input placeholder="Kinh độ" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="latitude" label="Latitude">
                <Input placeholder="Vĩ độ" />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item label="Video Files">
            <Dragger
              multiple
              accept="video/*,.mp4,.avi,.mov,.mkv,.wmv,.flv"
              fileList={fileList}
              beforeUpload={(file) => {
                setFileList(prev => [...prev, file as any]);
                return false; // Prevent auto upload
              }}
              onRemove={(file) => setFileList(prev => prev.filter(f => f.uid !== file.uid))}
            >
              <p className="ant-upload-drag-icon"><InboxOutlined /></p>
              <p className="ant-upload-text">Click hoặc kéo thả file vào đây</p>
              <p className="ant-upload-hint">Hỗ trợ: MP4, AVI, MOV, MKV, WMV, FLV</p>
            </Dragger>
          </Form.Item>

          <Form.Item>
            <Button type="primary" htmlType="submit" loading={uploading} size="large">
              Upload & Tạo Job
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </Space>
  );
}

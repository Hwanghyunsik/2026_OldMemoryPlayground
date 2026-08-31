using Unity.InferenceEngine;
using UnityEngine;

namespace Shinmyeong.Tracking
{
    /// MoveNet SinglePose Lightning(ONNX)으로 웹캠 프레임에서 17 키포인트를 추정한다.
    /// 원본 모델 입력은 int32 0~255 NHWC라서, float 0~1 입력을 받아 변환하는
    /// 래퍼 그래프를 Functional API로 만들어 사용한다.
    public class MoveNetPoseProvider : MonoBehaviour, IPoseProvider
    {
        [SerializeField] ModelAsset _modelAsset;
        [Tooltip("모델 입력 한 변 크기 — Lightning 192 · Thunder 256")]
        [SerializeField] int _inputSize = 192;
        [SerializeField] WebcamFeed _webcam;
        [SerializeField] BackendType _backend = BackendType.GPUCompute;

        Worker _worker;
        Tensor<float> _input;
        Tensor<float> _output;
        bool _inferenceInFlight;
        readonly PoseFrame _latest = new PoseFrame();
        TextureTransform _textureTransform;

        public bool IsRunning => _worker != null && _webcam != null && _webcam.IsReady;
        public Texture PreviewTexture => _webcam != null ? _webcam.Texture : null;

        /// 마지막 추론 1회에 걸린 시간(ms), 디버그 표시용
        public float LastInferenceMs { get; private set; }
        float _inferenceStartTime;

        void OnEnable()
        {
            if (_modelAsset == null)
            {
                Debug.LogError("[MoveNet] ModelAsset이 지정되지 않았습니다.");
                enabled = false;
                return;
            }
            if (_webcam == null)
                _webcam = GetComponent<WebcamFeed>();

            var sourceModel = ModelLoader.Load(_modelAsset);

            var graph = new FunctionalGraph();
            var image01 = graph.AddInput<float>(new TensorShape(1, _inputSize, _inputSize, 3));
            var image255 = Functional.Round(image01 * 255f).Int();
            var outputs = Functional.Forward(sourceModel, image255);
            graph.AddOutputs(outputs);

            _worker = new Worker(graph.Compile(), _backend);
            _input = new Tensor<float>(new TensorShape(1, _inputSize, _inputSize, 3));
            _textureTransform = new TextureTransform().SetTensorLayout(TensorLayout.NHWC);
        }

        void Update()
        {
            if (_worker == null || _webcam == null || !_webcam.IsReady)
                return;

            if (!_inferenceInFlight)
            {
                TextureConverter.ToTensor(_webcam.Texture, _input, _textureTransform);
                _worker.Schedule(_input);
                _output = _worker.PeekOutput() as Tensor<float>;
                _output.ReadbackRequest();
                _inferenceInFlight = true;
                _inferenceStartTime = Time.realtimeSinceStartup;
            }

            if (_inferenceInFlight && _output.IsReadbackRequestDone())
            {
                LastInferenceMs = (Time.realtimeSinceStartup - _inferenceStartTime) * 1000f;
                using (var result = _output.ReadbackAndClone())
                {
                    // 출력 [1,1,17,3] = (y, x, score) — 좌표는 입력 프레임 기준 0~1
                    for (int i = 0; i < PoseFrame.JointCount; i++)
                    {
                        float y = result[0, 0, i, 0];
                        float x = result[0, 0, i, 1];
                        float score = result[0, 0, i, 2];
                        _latest.Keypoints[i] = new PoseKeypoint
                        {
                            Position = new Vector2(x, y),
                            Score = score,
                        };
                    }
                    _latest.Timestamp = Time.realtimeSinceStartupAsDouble;
                    _latest.Valid = true;
                }
                _inferenceInFlight = false;
            }
        }

        public bool TryGetLatest(PoseFrame frame)
        {
            if (!_latest.Valid)
                return false;
            _latest.CopyTo(frame);
            return true;
        }

        void OnDisable()
        {
            _input?.Dispose();
            _worker?.Dispose();
            _worker = null;
            _latest.Valid = false;
        }
    }
}

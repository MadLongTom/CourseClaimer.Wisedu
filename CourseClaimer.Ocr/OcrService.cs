using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CourseClaimer.Ocr
{
    public class OcrService
    {

        /// <summary>
        /// 文字识别字符集
        /// </summary>
        private readonly string[] __charset;
        /// <summary>
        /// 模型路径
        /// </summary>
        private string __graph_path;
        /// <summary>
        /// det模式
        /// </summary>
        private bool det;

        InferenceSession __ort_session;

        public OcrService(bool ocr = true, bool det = false, bool old = false)
        {
            if (det)
            {
                ocr = false;
                Console.WriteLine("开启det后自动关闭ocr");
                this.__graph_path = Path.Combine(Directory.GetCurrentDirectory(), "common_det.onnx");
                this.__charset = new string[0]; 
                this.initData();
            }
            #region 初始化字符集
            if (ocr)
            {
                if (old)
                {
                    this.__graph_path = Path.Combine(Directory.GetCurrentDirectory(), "common_old.onnx");
                    this.__charset = Const.OCR_OLD_CHARSET;
                }
                else
                {
                    this.__graph_path = Path.Combine(Directory.GetCurrentDirectory(), "common.onnx");
                    this.__charset = Const.OCR_BETA_CHARSET; 
                }
            }
            #endregion 初始化字符集结束
            this.det = det;

            __ort_session = new InferenceSession(this.__graph_path, new SessionOptions());

        }

        /// <summary>
        /// 目标检测
        /// </summary>
        /// <param name="img_bytes"></param>
        /// <param name="img_base64"></param>
        /// <param name="minwidth">最小检测宽度</param>
        /// <param name="minheight">最小检测高度</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public List<int[]> detection(byte[] img_bytes = null, string img_base64 = null,int minwidth = -1,int minheight=-1)
        {
            if (!det)
            {
                throw new Exception("当前识别类型为文字识别");
            }
            if (img_bytes == null)
            {
                img_bytes = Convert.FromBase64String(img_base64);
            }
            var mat = Mat.ImDecode(img_bytes, ImreadModes.Color);
            var result = detection(mat, minwidth,minheight);
            return result;
        }

        public List<int[]> detection(Mat image, int minwidth = -1, int minheight = -1)
        {
            if (!det)
            {
                throw new Exception("当前识别类型为文字识别");
            }
            var result = this.get_bbox(image,minwidth,minheight);
            return result;
        } 

        /// <summary>
        /// 识别文字
        /// </summary>
        /// <param name="img_bytes">图像数据</param>
        /// <param name="img_base64">base64的图像数据</param>
        /// <param name="label_map">自定义限制字符集</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string classification(byte[] img_bytes = null, string img_base64 = null,string[] label_map = null)
        { 
            if (det)
            {
                throw new Exception("当前识别类型为目标检测");
            }
            if (img_bytes == null)
            {
                img_bytes = Convert.FromBase64String(img_base64);
            } 
            Mat image = Mat.ImDecode(img_bytes, ImreadModes.Color); 
            return classification(image, label_map);
        }

        /// <summary>
        /// 直接读取
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string classification(Mat image,string[] label_map = null)
        {
            if (det)
            {
                throw new Exception("当前识别类型为目标检测");
            }

            image = image.Resize(new OpenCvSharp.Size(image.Width * 64 / image.Height, 64), interpolation: InterpolationFlags.Nearest);
            if (image.Channels() > 1)
            {
                image = image.CvtColor(ColorConversionCodes.BGR2GRAY);
            }  
            var inputs = new NamedOnnxValue[] // add image as onnx input
               {
                    NamedOnnxValue.CreateFromTensor("input1", ExtractPixels2(image))
               };

            var outputs = new string[]
            {
                "output"
            }; 

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> result = __ort_session.Run(inputs, outputs); // run inference 

            var predictions = result.First(x => x.Name == "output").Value as DenseTensor<float>;

            int outsize = (int)(predictions.Length / this.__charset.Length);
            int[] outdata = new int[outsize];

            var excludeindex = new List<int>();

            if (label_map != null && label_map.Length > 0)
            {
                var includeindex = new List<int>();
                for (int i = 0; i < label_map.Length; i++)
                {
                    string nowlabel = label_map[i];
                    int nowindex = Array.IndexOf(this.__charset,nowlabel);
                    includeindex.Add(nowindex);
                }

                for (int i = 0; i < this.__charset.Length - 1; i++)
                {
                    excludeindex.Add(i + 1);
                }
                foreach (int i in includeindex)
                {
                    excludeindex.Remove(i);
                }
            }
            for (int i = 0; i < outsize; i++)
            {
                int skip = i * this.__charset.Length;
                var tempdata = predictions.Skip(skip).Take(this.__charset.Length).ToList();

                foreach (int j in excludeindex)
                {
                    tempdata[j] = 0;
                }
                var index = tempdata.IndexOf(tempdata.Max());
                outdata[i] = index;
            }
            StringBuilder res = new StringBuilder();
            foreach (float prediction in outdata)
            {
                res.Append(this.__charset[(int)prediction]);
            }
            return res.ToString();
        }

        /// <summary>
        /// Extracts pixels into tensor for net input.
        /// </summary>
        unsafe Tensor<float> ExtractPixels2(Mat source)
        {
            int width = source.Width;
            int height = source.Height;

            Tensor<float> data = new DenseTensor<float>(new[] { 1, 1, height, width });

            for (int y = 0; y < height; y++)
            {
                IntPtr rowPtr = source.Ptr(y);
                byte* rowBytePtr = (byte*)rowPtr.ToPointer();

                for (int x = 0; x < width; x++)
                {
                    data[0, 0, y, x] = (float)((((float)rowBytePtr[x] / 255f) - 0.5f) / 0.5f);
                }
            }

            return data;
        }

        const int _modelHeight = 416;
        const int _modelWidth = 416;
        static Scalar grayScalar = new Scalar(114, 114, 114);

        // 创建一个新的Mat对象，大小为_modelWidth x _modelHeight
        Mat ret = new Mat(_modelWidth, _modelHeight, MatType.CV_8UC3, grayScalar);

        /// <summary>
        /// Resizes image keeping ratio to fit model input size. 
        /// </summary>
        Mat ResizeImage(Mat image)
        {
            int w = image.Width, h = image.Height; // image width and height

            float ratio = Math.Min(_modelWidth / (float)w, _modelHeight / (float)h); // resize ratio
            int width = (int)(w * ratio);
            int height = (int)(h * ratio); // resized width and height

            ret.SetTo(grayScalar);

            Size newSize = new Size(width, height);
            Rect roi = new Rect(0, 0, width, height);

            // 调整大小并将结果存储在目标Mat对象中
            Cv2.Resize(image, ret[roi], newSize);

            return ret;
        }

        /// <summary>
        /// Extracts pixels into tensor for net input.
        /// </summary>    
        unsafe Tensor<float> ExtractPixels(Mat source)
        {
            int width = source.Width;
            int height = source.Height;
            int channels = source.Channels();
            var tensor = new DenseTensor<float>(new[] { 1, 3, height, width }); // 根据实际需要调整第一个维度的大小

            byte[] pixelData = new byte[width * height * channels];
            GCHandle handle = GCHandle.Alloc(pixelData, GCHandleType.Pinned);

            // 将Mat对象中的像素数据复制到一维数组中
            Marshal.Copy(source.Data, pixelData, 0, pixelData.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * channels;

                    // 获取BGR通道的值，注意通道顺序可能需要根据实际情况调整 
                    tensor[0, 0, y, x] = pixelData[index]; //blue
                    tensor[0, 1, y, x] = pixelData[index + 1]; //green
                    tensor[0, 2, y, x] = pixelData[index + 2]; //red
                };
            }

            handle.Free();

            return tensor;
        }

        /// <summary>
        /// Returns value clamped to the inclusive range of min and max.
        /// </summary>
        float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        int[] makeGrid(int a, int b)
        {
            int[] ret = new int[a * b * 2];

            for (int i = 0; i < a; i++)
            {
                for (int j = 0; j < b; j += 1)
                {
                    int index = (i * a + j) * 2;
                    ret[index] = j;
                    ret[index + 1] = i;
                }
            }
            return ret;
        }

        int[] makeexpanded_stride(int a, int b)
        {
            int[] ret = new int[a];

            for (int i = 0; i < a; i++)
            {
                ret[i] = b;
            }
            return ret;
        }

        int[] grids = null;
        int[] expanded_strides = null;
        private void initData()
        {
            if (grids!=null)
            {
                return;
            }

            List<int> tmpgrids=new List<int>();
            List<int> tmpexpanded_strides = new List<int>();
            int[] strides = new int[] { 8, 16, 32 };
            //int[] hsizes = strides.Select(p => (int)(_modelHeight / p)).ToArray();
            //int[] wsizes = strides.Select(p => (int)(_modelWidth / p)).ToArray();
            int[] hsizes = new int[] { 52, 26, 13 };
            int[] wsizes = new int[] { 52, 26, 13 };

            for (int i = 0; i < strides.Length; i++)
            {
                int hsize = hsizes[i], wsize = wsizes[i], stride = strides[i];
                var grid = makeGrid(hsize, wsize);
                var expanded_stride = makeexpanded_stride(hsize * wsize, stride);
                tmpgrids.AddRange(grid);
                tmpexpanded_strides.AddRange(expanded_stride);
            }
            grids = tmpgrids.ToArray();
            expanded_strides = tmpexpanded_strides.ToArray();
        }


        /// <summary>
        /// 目标识别的多类别NMSBoxs并返回识别后的目标Rect坐标
        /// </summary>
        /// <param name="output">DenseTensor识别数据</param>
        /// <param name="image">识别图片</param>
        /// <returns></returns>
        private List<DetResult> ParseDetect(DenseTensor<float>? output, Mat image, int minwidth = -1, int minheight = -1)
        {
            return DetectHandleProcessing(output, image, new Size(_modelWidth, _modelHeight), minwidth: minwidth, minheight: minheight);
        }

        /// <summary>
        /// 目标识别的多类别NMSBoxs并返回识别后的目标Rect坐标
        /// </summary>
        /// <param name="output">DenseTensor识别数据</param>
        /// <param name="image">识别图片</param>
        /// <param name="img_size">默认图片大小</param>
        /// <param name="p6"></param>
        /// <param name="nms_thr">nms参数</param>
        /// <param name="score_thr">score参数</param>
        /// <returns></returns>
        private List<DetResult> DetectHandleProcessing(DenseTensor<float>? output, Mat image, Size img_size, float nms_thr = 0.45f, float score_thr = 0.1f, int minwidth = -1, int minheight = -1)
        {
            if (output == null)
                return new();

            List<float> scoreslist = new();
            List<Rect> bboxs = new();
            List<DetResult> result = new();

            var (w, h) = (image.Width, image.Height); // image w and h
            var (xGain, yGain) = (img_size.Width / (float)w, img_size.Height / (float)h); // x, y gains
            var gain = Math.Min(xGain, yGain); // gain = resized / original
            int len = (int)(output.Length / 6);
            for (int i = 0; i < len; i++)
            {
                float scores = output[0, i, 4] * output[0, i, 5];

                float x1 = output[0, i, 0];

                float y1 = output[0, i, 1];

                float x2 = output[0, i, 2];

                float y2 = output[0, i, 3];

                x1 = (x1 + grids[i * 2 + 0]) * expanded_strides[i];
                y1 = (y1 + grids[i * 2 + 1]) * expanded_strides[i];
                x2 = (float)(Math.Exp(x2) * expanded_strides[i]);
                y2 = (float)(Math.Exp(y2) * expanded_strides[i]);

                float x11 = (x1 - x2 / 2) / gain;
                float y11 = (y1 - y2 / 2) / gain;
                float x22 = (x1 + x2 / 2) / gain;
                float y22 = (y1 + y2 / 2) / gain;

                int width = (int)(x22 - x11);
                int height = (int)(y22 - y11);
                if (width < minwidth || height < minheight)
                {
                    continue;
                }
                scoreslist.Add(scores);
                bboxs.Add(new Rect((int)x11, (int)y11, width, height));

            }

            CvDnn.NMSBoxes(bboxs, scoreslist, score_thr, nms_thr, out var indices);
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                result.Add(new DetResult(scoreslist[index], bboxs[index].Left, bboxs[index].Top, bboxs[index].Right, bboxs[index].Bottom));
            }
            return result;
        }

        private bool islike(int[] p, int[] t)
        {
            int gap = 5;
            int diff;
            for (int i = 0; i < p.Length; i++)
            {
                diff = Math.Abs(p[i] - t[i]);
                if (diff > gap) return false;
            }

            return true;
        }

        private List<int[]> get_bbox(Mat imagemat, int minwidth = -1, int minheight = -1)
        {
            Mat resized = ResizeImage(imagemat);

            var inputs = new NamedOnnxValue[] // add image as onnx input
                {
                    NamedOnnxValue.CreateFromTensor("images", ExtractPixels(resized ?? imagemat))
                };

            var outputs = new string[]
            {
                "output"
            };

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> result = __ort_session.Run(inputs,outputs); // run inference


            var predictions = ParseDetect(result.First(x => x.Name == "output").Value as DenseTensor<float>, imagemat, minwidth, minheight);
            List<int[]> ret = new List<int[]>();

            foreach (var prediction in predictions)
            {
                var t = new int[] { (int)prediction.x1, (int)prediction.y1, (int)prediction.x2, (int)prediction.y2 };
                if (ret.Where(p => islike(p, t)).Count() == 0)
                {
                    ret.Add(t);
                }
            }
            return ret;
        }


        #region slide
        /// <summary>
        /// 滑块缺口识别算法1
        /// </summary>
        /// <param name="target">缺口背景图片</param>
        /// <param name="background">背景图片</param>
        /// <returns>返回缺口左上角坐标</returns>
        public static Point Slide_Comparison(Mat target, Mat background)
        {
            int start_x = 0, start_y = 0;
            // 将图像转换为灰度图，以便进行差异计算

            background = background.CvtColor(ColorConversionCodes.BGR2GRAY);
            target = target.CvtColor(ColorConversionCodes.BGR2GRAY);

            // 计算灰度图像的差异  
            Mat difference = new();
            Cv2.Absdiff(background, target, difference); // 使用OpenCV的AbsDiff方法来计算差异  

            difference = difference.Threshold(80, 255, ThresholdTypes.Binary);
            for (var i = 0; i < difference.Width; i++)
            {
                int mcount = 0;
                for (var j = 0; j < difference.Height; j++)
                {
                    var p = difference.Get<Vec3b>(j, i);
                    if (p.Item2 != 0)
                    {
                        mcount += 1;
                    }
                    if (mcount >= 5 && start_y == 0)
                    {
                        start_y = j - 5;
                    }
                }
                if (mcount > 5)
                {
                    start_x = i + 2;
                    break;
                }
            }
            Point point = new(start_x, start_y);
            return point;
        }

        /// <summary>
        /// 滑块位置识别算法2
        /// </summary>
        /// <param name="targetMat">滑块图片</param>
        /// <param name="backgroundMat">带缺口背景图片</param>
        /// <param name="target_y">缺口Y轴坐标</param>
        /// <param name="simpleTarget"></param>
        /// <param name="flag">报错标识</param>
        /// <returns></returns>

        public static (int, Rect) SlideMatch(Mat targetMat, Mat backgroundMat, int target_y = 0, bool simpleTarget = false, bool flag = false)
        {
            Mat target;
            Mat lockTarget;
            Point targetPoint = default;
            int mTarget_Y = 0;
            if (!simpleTarget)
            {
                try
                {
                    // Assuming GetTarget is a method that returns a Bitmap, targetX and targetY
                    (target, targetPoint) = GetTarget(targetMat);
                }
                catch (Exception)
                {
                    if (flag)
                    {
                        throw;
                    }
                    return SlideMatch(targetMat, backgroundMat, target_y, true, true);
                }
            }
            else
            {
                target = Cv2.ImDecode(targetMat.ImEncode(), ImreadModes.AnyColor);
            }

            lockTarget = target;

            Mat background = Cv2.ImDecode(backgroundMat.ImEncode(), ImreadModes.AnyColor);
            mTarget_Y = targetPoint.Y + target_y;
            if (targetPoint != default)
            {
                ;
                background = background.Clone(new Rect(0, mTarget_Y, backgroundMat.Width, backgroundMat.Height - mTarget_Y));
            }
            Mat cbackground = new();
            Mat ctarget = new();

            background = background.GaussianBlur(new Size(3, 3), 3).CvtColor(ColorConversionCodes.BGR2GRAY).Threshold(128, 255, ThresholdTypes.Binary).MedianBlur(3);
            target = target.GaussianBlur(new Size(3, 3), 0.5).CvtColor(ColorConversionCodes.BGR2GRAY).Threshold(128, 255, ThresholdTypes.Binary);
            var kernel1 = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5), new OpenCvSharp.Point(-1, -1));
            Cv2.MorphologyEx(background, background, MorphTypes.Close, kernel1);
            Cv2.MorphologyEx(target, target, MorphTypes.Close, kernel1);

            Cv2.Canny(background, cbackground, 100, 200);
            Cv2.Canny(target, ctarget, 100, 200);

            Cv2.CvtColor(cbackground, background, ColorConversionCodes.GRAY2BGR);
            Cv2.CvtColor(ctarget, target, ColorConversionCodes.GRAY2BGR);
            //#if DEBUG
            //            Cv2.ImShow("background1", background);
            //            Cv2.ImShow("target", ctarget);
            //            //Cv2.WaitKey(0);
            //#endif
            Mat res = new();
            Cv2.MatchTemplate(background, target, res, TemplateMatchModes.CCoeffNormed);

            Cv2.MinMaxLoc(res, out _, out _, out _, out Point maxLoc);

            var mRect = new Rect(maxLoc.X, simpleTarget == true ? maxLoc.Y : mTarget_Y, lockTarget.Width, lockTarget.Height);

            return (mTarget_Y, mRect);
        }


        /// <summary>
        /// 获取滑块精确方块内容
        /// </summary>
        /// <param name="image">滑块图片字节集</param>
        /// <returns></returns>
        public static (Mat, Point) GetTarget(Mat image)
        {
            Mat Result = new Mat(image.Size(), MatType.CV_8UC3, Scalar.White);
            Point point = new Point();

            Mat bw = new Mat();

            bw = image.GaussianBlur(new Size(3, 3), 3).CvtColor(ColorConversionCodes.BGR2GRAY).Threshold(128, 255, ThresholdTypes.Binary);
            Mat ctarget = new();
            Cv2.Canny(bw, ctarget, 100, 300);
            //Cv2.ImShow("Canny Contours", ctarget);
            OpenCvSharp.Point[][] contours;
            Cv2.FindContours(ctarget, out contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            double maxArea = 0;
            // 筛选矩形轮廓
            for (var i = 0; i < contours.Length; i++)
            {

                var rect = Cv2.BoundingRect(contours[i]);
                if (rect.Width * rect.Height > maxArea && Convert.ToDouble(rect.Width) / image.Width > 0.5)
                {
                    maxArea = rect.Width * rect.Height;

                    point.X = rect.X; point.Y = rect.Y;

                    Result = image.Clone(rect);

                }

            }

            return (Result, point);
        }
        #endregion

    }
}

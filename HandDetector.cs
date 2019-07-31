using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.DnnModule;

public delegate void HandDel(Mat image, int imgWidth, int imgHeight,ref BoxOutline ouline);
public class BoxOutline
{
    public float XMin;
    public float XMax;
    public float YMin;
    public float YMax;
}

public class HandDetector : MonoBehaviour
{
    public WebCamOperation webCamOperation;
    // [0]:opencv [1]:tensorflow [2]:contour [3]:cascade
    public int handDetectionMode;

    Net tfDetector;
    CascadeClassifier cascadeDetector;
    HandDel del;
    int imgWidth;
    int imgHeight;


    [Header("UI")]
    public RawImage HandImage;
    Texture2D HandTexture;
    Mat HandMat;
    BoxOutline Outline;

    private void LoadDetector()
    {
        if (handDetectionMode == 0)
        {
            var cascadeFileName = Utils.getFilePath("palm.xml");
            cascadeDetector = new CascadeClassifier();
            cascadeDetector.load(cascadeFileName);
            if (cascadeDetector.empty())
            {
                Debug.LogError("cascade file is not loaded. Please copy from “OpenCVForUnity/StreamingAssets/” to “Assets/StreamingAssets/” folder. ");
            }
        }
        else if (handDetectionMode == 2)
        {

            var modelPath = Utils.getFilePath("frozen_inference_graph.pb");
            var configPath = Utils.getFilePath("frozen_inference_graph.pbtxt");
            tfDetector = Dnn.readNetFromTensorflow(modelPath, configPath);
            if (tfDetector.empty())
            {
                Debug.Log("tf detector is empty");
            }
        }
    }

    bool initialized = false;

    void init()
    {
        imgWidth = webCamOperation.GetWebCamTexture().width;
        imgHeight = webCamOperation.GetWebCamTexture().height;

        // Define the texture
        HandTexture = new Texture2D(imgWidth, imgHeight);
        HandImage.texture = HandTexture;
        HandMat = new Mat(imgHeight, imgWidth, CvType.CV_8UC3);
        
        LoadDetector();

        initialized = true;
    }
    void Start()
    {
        if (handDetectionMode == 0)
        {
            del = new HandDel(this.CascadeDetect);
        }
        else if (handDetectionMode == 1)
        {
            del = new HandDel(this.ContourDetect);
        }
        else if (handDetectionMode == 2)
        {
            del = new HandDel(this.TFDetect);
        }
    }

    void Update()
    {
        if (!webCamOperation.WebCamRunning())
            return;
        if (!initialized)
        {
            init();
        }
        HandMat = webCamOperation.GetMat();
        del.Invoke(HandMat, imgWidth, imgHeight, ref Outline);
        PostAction(HandTexture, HandMat, Outline);
    }

    void CascadeDetect(Mat image, int imgWidth, int imgHeight, ref BoxOutline outline)
    {
        MatOfRect hands = new MatOfRect();
        Mat gray = new Mat(imgHeight, imgWidth, CvType.CV_8UC3);
        Imgproc.cvtColor(image, gray, Imgproc.COLOR_BGR2GRAY);
        Imgproc.equalizeHist(gray, gray);

        cascadeDetector.detectMultiScale(
            gray,
            hands,
            1.1,
            2,
            0 | Objdetect.CASCADE_DO_CANNY_PRUNING | Objdetect.CASCADE_SCALE_IMAGE | Objdetect.CASCADE_FIND_BIGGEST_OBJECT,
            new Size(10, 10),
            new Size());

        OpenCVForUnity.CoreModule.Rect[] handsArray = hands.toArray();
        if (handsArray.Length != 0)
        {
            outline = new BoxOutline
            {
                XMin = (float)handsArray[0].x,
                XMax = (float)handsArray[0].x+handsArray[0].width,
                YMin = (float)handsArray[0].y,
                YMax = (float)handsArray[0].y + handsArray[0].height
            };
            Debug.Log("cascade: palm detected!");
        }
        else
        {
            outline = null;
        }
    }

    void ContourDetect(Mat image, int imgWidth, int imgHeight, ref BoxOutline outline)
    {
        // filter skin color
        var output_mask = GetSkinMask(image, imgWidth, imgHeight);

        // find the convex hull of finger
        int cx = -1, cy = -1;
        FindDefects(output_mask, ref cx, ref cy, 1, 4);
        if (cx == -1 && cy == -1)
        {
            outline = null;
            return;
        }
        outline = new BoxOutline
        {
            XMin = (float)cx - 15,
            XMax = (float)cx + 15,
            YMin = (float)cy - 15,
            YMax = (float)cy + 15
        };
    }

    private void TFDetect(Mat image, int imgWidth, int imgHeight, ref BoxOutline outline)
    {
        if (image == null)
        {
            Debug.Log("unable to find colors");
            return;
        }

        var blob = Dnn.blobFromImage(image, 1, new Size(300, 300), new Scalar(0, 0, 0), true, false);
        tfDetector.setInput(blob);
        Mat prob = tfDetector.forward();
        Mat newMat = prob.reshape(1, (int)prob.total() / prob.size(3));
        float maxScore = 0;
        int scoreInd = 0;
        for (int i = 0; i < newMat.rows(); i++)
        {
            var score = (float)newMat.get(i, 2)[0];
            if (score > maxScore)
            {
                maxScore = score;
                scoreInd = i;
            }
        }
        //Debug.Log(maxScore);
        if (maxScore > 0.7)
        {
            float left = (float)(newMat.get(scoreInd, 3)[0] * imgWidth);
            float top = (float)(newMat.get(scoreInd, 4)[0] * imgHeight);
            float right = (float)(newMat.get(scoreInd, 5)[0] * imgWidth);
            float bottom = (float)(newMat.get(scoreInd, 6)[0] * imgHeight);

            left = (int)Mathf.Max(0, Mathf.Min(left, imgWidth - 1));
            top = (int)Mathf.Max(0, Mathf.Min(top, imgHeight - 1));
            right = (int)Mathf.Max(0, Mathf.Min(right, imgWidth - 1));
            bottom = (int)Mathf.Max(0, Mathf.Min(bottom, imgHeight - 1));

            outline = new BoxOutline
            {
                XMin = right,
                XMax = left,
                YMin = bottom,
                YMax = top
            };
        }
        else
        {
            outline = null;
        }
        prob.Dispose();
        newMat.Dispose();
    }

    void PostAction(Texture2D tex, Mat mat, BoxOutline outl)
    {
        if (outl != null)
        {
            Imgproc.rectangle(mat, new Point(outl.XMin, outl.YMin), new Point(outl.XMax, outl.YMax), new Scalar(255, 0, 0));
        }
        Utils.matToTexture2D(mat, tex);
    }

    private Mat GetSkinMask(Mat imgMat, int imgWidth, int imgHeight)
    {
        Mat YCrCb_image = new Mat();
        int Y_channel = 0;
        int Cr_channel = 1;
        int Cb_channel = 2;
        Imgproc.cvtColor(imgMat, YCrCb_image, Imgproc.COLOR_RGB2YCrCb);

        // zero mat
        var output_mask = Mat.zeros(imgWidth, imgHeight, CvType.CV_8UC1);

        for (int i = 0; i < YCrCb_image.rows(); i++)
        {
            for (int j = 0; j < YCrCb_image.cols(); j++)
            {
                double[] p_src = YCrCb_image.get(i, j);

                if (p_src[Y_channel] > 80 &&
                    p_src[Cr_channel] > 135 &&
                    p_src[Cr_channel] < 180 &&
                    p_src[Cb_channel] > 85 &&
                    p_src[Cb_channel] < 135)
                {
                    output_mask.put(i, j, 255);
                }
            }
        }
        YCrCb_image.Dispose();
        return output_mask;
    }

    private void FindDefects(Mat maskImage, ref int cx, ref int cy, int min_defects_count, int max_defects_count)
    {
        int erosion_size = 1;

        Mat element = Imgproc.getStructuringElement(
            Imgproc.MORPH_ELLIPSE,
            new Size(2 * erosion_size + 1, 2 * erosion_size + 1),
            new Point(erosion_size, erosion_size));

        // dilate and erode
        Imgproc.dilate(maskImage, maskImage, element);
        Imgproc.erode(maskImage, maskImage, element);
        element.Dispose();
        //Find Contours in image
        List<MatOfPoint> contours = new List<MatOfPoint>();
        Imgproc.findContours(maskImage, contours, new MatOfPoint(), Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

        //Loop to find the biggest contour; If no contour is found index=-1
        int index = -1;
        double area = 2000;
        for (int i = 0; i < contours.Count; i++)
        {
            var tempsize = Imgproc.contourArea(contours[i]);
            if (tempsize > area)
            {
                area = tempsize;
                index = i;
            }
        }

        if (index == -1)
        {
            return;
        }
        else
        {
            var points = new MatOfPoint(contours[index].toArray());
            var hull = new MatOfInt();
            Imgproc.convexHull(points, hull, false);

            var defects = new MatOfInt4();
            Imgproc.convexityDefects(points, hull, defects);

            var start_points = new MatOfPoint2f();
            var far_points = new MatOfPoint2f();

            for (int i = 0; i < defects.size().height; i++)
            {
                int ind_start = (int)defects.get(i, 0)[0];
                int ind_end = (int)defects.get(i, 0)[1];
                int ind_far = (int)defects.get(i, 0)[2];
                double depth = defects.get(i, 0)[3] / 256;

                double a = Core.norm(contours[index].row(ind_start) - contours[index].row(ind_end));
                double b = Core.norm(contours[index].row(ind_far) - contours[index].row(ind_start));
                double c = Core.norm(contours[index].row(ind_far) - contours[index].row(ind_end));

                double angle = Math.Acos((b * b + c * c - a * a) / (2 * b * c)) * 180.0 / Math.PI;

                double threshFingerLength = ((double)maskImage.height()) / 8.0;
                double threshAngle = 80;

                if (angle < threshAngle && depth > threshFingerLength)
                {
                    //start point
                    var aa = contours[index].row(ind_start);
                    start_points.push_back(contours[index].row(ind_start));
                    far_points.push_back(contours[index].row(ind_far));
                }
            }

            points.Dispose();
            hull.Dispose();
            defects.Dispose();

            // when no finger found
            if (far_points.size().height < min_defects_count || far_points.size().height > max_defects_count)
            {
                return;
            }

            var cnts = new List<MatOfPoint>();
            cnts.Add(contours[index]);

            Mat mm = new Mat();
            Imgproc.cvtColor(maskImage, mm, Imgproc.COLOR_GRAY2BGR);

            Imgproc.drawContours(mm, cnts, 0, new Scalar(0, 0, 255));
            // OpenCVForUnity.ImgcodecsModule.Imgcodecs.imwrite("D:/tempImg.jpg", mm)

            //var rotatedRect = Imgproc.minAreaRect(far_points);
            var boundingRect = Imgproc.boundingRect(far_points);

            cx = (int)(boundingRect.x + boundingRect.width / 2);
            cy = (int)(boundingRect.y + boundingRect.height / 2);
        }

    }

}

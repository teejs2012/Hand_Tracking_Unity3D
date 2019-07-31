using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


/// <summary>
/// WebCamTexture Example
/// An example of detecting face landmarks in WebCamTexture images.
/// </summary>
public class WebCamOperation : MonoBehaviour
{
    //public RawImage rawImage;

    [SerializeField, TooltipAttribute("Hide the image or not")]
    public bool isHideCameraImage = true;

    /// <summary>
    /// Set the name of the device to use.
    /// </summary>
    //[SerializeField, TooltipAttribute("Set the name of the device to use.")]
    private string requestedDeviceName = null;

    /// <summary>
    /// Set the width of WebCamTexture.
    /// </summary>
    [SerializeField, TooltipAttribute("Set the width of WebCamTexture.")]
    public int requestedWidth = 1280;

    /// <summary>
    /// Set the height of WebCamTexture.
    /// </summary>
    [SerializeField, TooltipAttribute("Set the height of WebCamTexture.")]
    public int requestedHeight = 760;

    /// <summary>
    /// Set FPS of WebCamTexture.
    /// </summary>
    [SerializeField, TooltipAttribute("Set FPS of WebCamTexture.")]
    public int requestedFPS = 30;

    /// <summary>
    /// Set whether to use the front facing camera.
    /// </summary>
    private bool requestedIsFrontFacing = true;

    ///// <summary>
    ///// Determines if adjust pixels direction.
    ///// </summary>
    //[SerializeField, TooltipAttribute("Determines if adjust pixels direction.")]
    private bool adjustPixelsDirection = true;

    /// <summary>
    /// The webcam texture.
    /// </summary>
    WebCamTexture webCamTexture;

    /// <summary>
    /// The webcam device.
    /// </summary>
    WebCamDevice webCamDevice;

    /// <summary>
    /// The colors.
    /// </summary>
    Color32[] colors;

    /// <summary>
    /// Indicates whether this instance is waiting for initialization to complete.
    /// </summary>
    bool isInitWaiting = false;

    /// <summary>
    /// Indicates whether this instance has been initialized.
    /// </summary>
    bool hasInitDone = false;

    /// <summary>
    /// The width of the screen.
    /// </summary>
    int screenWidth;

    /// <summary>
    /// The height of the screen.
    /// </summary>
    int screenHeight;

    /// <summary>
    /// The texture.
    /// </summary>
    Texture2D texture;

    OpenCVForUnity.CoreModule.Mat mat;

    // Use this for initialization
    void Awake()
    {
        Initialize();
    }

    void Initialize()
    {
        if (isInitWaiting)
            return;
        StartCoroutine(_Initialize());
    }

    public WebCamTexture GetWebCamTexture()
    {
        return webCamTexture;
    }

    public bool WebCamRunning()
    {
        return hasInitDone && webCamTexture.isPlaying;
    }
    /// <summary>
    /// Initializes webcam texture by coroutine.
    /// </summary>
    private IEnumerator _Initialize()
    {
        if (hasInitDone)
            Dispose();

        isInitWaiting = true;

        // Creates the camera

        webCamDevice = WebCamTexture.devices[0];
        webCamTexture = new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight, requestedFPS);

        if (webCamTexture == null)
        {
            Debug.Log("Cannot find camera device " + requestedDeviceName + ".");
        }

        if (webCamTexture == null)
        {
            // Checks how many and which cameras are available on the device
            for (int cameraIndex = 0; cameraIndex < WebCamTexture.devices.Length; cameraIndex++)
            {
                if (WebCamTexture.devices[cameraIndex].isFrontFacing == requestedIsFrontFacing)
                {
                    webCamDevice = WebCamTexture.devices[cameraIndex];
                    webCamTexture = new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight, requestedFPS);
                    break;
                }
            }
        }

        if (webCamTexture == null)
        {
            if (WebCamTexture.devices.Length > 0)
            {
                webCamDevice = WebCamTexture.devices[0];
                webCamTexture = new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight, requestedFPS);
            }
            else
            {
                Debug.LogError("Camera device does not exist.");
                isInitWaiting = false;
                yield break;
            }
        }

        //rawImage.material.mainTexture = webCamTexture;

        // Starts the camera
        webCamTexture.Play();

        while (true)
        {
            if (webCamTexture.didUpdateThisFrame)
            {

                Debug.Log("name:" + webCamTexture.deviceName + " width:" + webCamTexture.width + " height:" + webCamTexture.height + " fps:" + webCamTexture.requestedFPS);
                Debug.Log("videoRotationAngle:" + webCamTexture.videoRotationAngle + " videoVerticallyMirrored:" + webCamTexture.videoVerticallyMirrored + " isFrongFacing:" + webCamDevice.isFrontFacing);

                screenWidth = Screen.width;
                screenHeight = Screen.height;
                isInitWaiting = false;
                hasInitDone = true;

                OnInited();

                break;
            }
            else
            {
                yield return 0;
            }
        }
    }

    /// <summary>
    /// Releases all resource.
    /// </summary>
    private void Dispose()
    {
        isInitWaiting = false;
        hasInitDone = false;

        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            WebCamTexture.Destroy(webCamTexture);
            webCamTexture = null;
        }
        if (texture != null)
        {
            Texture2D.Destroy(texture);
            texture = null;
        }
    }

    /// <summary>
    /// Raises the webcam texture initialized event.
    /// </summary>
    private void OnInited()
    {
        if (colors == null || colors.Length != webCamTexture.width * webCamTexture.height)
        {
            colors = new Color32[webCamTexture.width * webCamTexture.height];
        }

        texture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);
        mat = new OpenCVForUnity.CoreModule.Mat(webCamTexture.height, webCamTexture.width, OpenCVForUnity.CoreModule.CvType.CV_8UC3);
        //rawImage.texture = texture;

        gameObject.transform.localScale = new Vector3(texture.width, texture.height, 1);
        Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

        float width = texture.width;
        float height = texture.height;

        float widthScale = (float)Screen.width / width;
        float heightScale = (float)Screen.height / height;
    }

    // Update is called once per frame
    void Update()
    {
        if (adjustPixelsDirection)
        {
            // Catch the orientation change of the screen.
            if (screenWidth != Screen.width || screenHeight != Screen.height)
            {
                Initialize();
            }
            else
            {
                screenWidth = Screen.width;
                screenHeight = Screen.height;
            }
        }

        if (hasInitDone && webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
        {
            int webCamTexture_width = webCamTexture.width;
            int webCamTexture_height = webCamTexture.height;

            // Color32 format
            Color32[] colors = GetColors();

            if (colors != null && colors.Length != 0)
            {
                texture.SetPixels32(colors);
                texture.Apply(false);
                // Convert the texture2d to mat format
                OpenCVForUnity.UnityUtils.Utils.texture2DToMat(texture, mat);
                return;
            }
        }
    }

    /// <summary>
    /// Gets the current WebCameraTexture frame that converted to the correct direction.
    /// </summary>
    public Color32[] GetColors()
    {
        webCamTexture.GetPixels32(colors);

        if (adjustPixelsDirection)
        {
            //Adjust an array of color pixels according to screen orientation and WebCamDevice parameter.
            FlipColors<Color32>(colors, webCamTexture.width, webCamTexture.height);
            return colors;
        }
        return colors;
    }

    /// <summary>
    /// Gets the current WebCameraTexture frame that converted to the correct direction.
    /// </summary>
    public Color[] GetColors(int x, int y, int width, int height)
    {
        // The (0,0) coordinate of unity is right-bottom corner
        int x_RightBottom = webCamTexture.width - width - x;
        int y_RightBottom = webCamTexture.height - height - y;

        Color[] tempColors = webCamTexture.GetPixels(x_RightBottom, y_RightBottom, width, height);

        if (adjustPixelsDirection)
        {
            //Adjust an array of color pixels according to screen orientation and WebCamDevice parameter.
            FlipColors<Color>(tempColors, width, height);
            return tempColors;
        }
        return tempColors;
    }

    public OpenCVForUnity.CoreModule.Mat GetMat()
    {
        // Get the full image
        var colors = GetColors();
        var texture = new Texture2D(webCamTexture.width, webCamTexture.height);
        texture.SetPixels32(colors);

        // Convert the texture2d to mat format
        var dst = new OpenCVForUnity.CoreModule.Mat(webCamTexture.height, webCamTexture.width, OpenCVForUnity.CoreModule.CvType.CV_8UC3);
        OpenCVForUnity.UnityUtils.Utils.texture2DToMat(texture, dst);

        // Validate the mat object
        if (dst == null || dst.getNativeObjAddr() == System.IntPtr.Zero)
            return null;

        return dst;
    }

    public OpenCVForUnity.CoreModule.Mat GetMat(int x, int y, int width, int height)
    {
        if (mat == null || mat.getNativeObjAddr() == System.IntPtr.Zero)
            return null;

        // Crop the image by using opencv
        var rect = new OpenCVForUnity.CoreModule.Rect(x, y, width, height);
        var dst = new OpenCVForUnity.CoreModule.Mat(mat, rect);

        // Validate the mat object
        if (dst == null || dst.getNativeObjAddr() == System.IntPtr.Zero)
            return null;

        return dst;
    }

    /// <summary>
    /// Raises the destroy event.
    /// </summary>
    void OnDestroy()
    {
        Dispose();
    }

    /// <summary>
    /// Flips the colors.
    /// </summary>
    /// <param name="colors">Colors.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    void FlipColors<T>(IList<T> colors, int width, int height)
    {
        int flipCode = int.MinValue;

        if (webCamDevice.isFrontFacing)
        {
            if (webCamTexture.videoRotationAngle == 0)
            {
                flipCode = 1;
            }
            else if (webCamTexture.videoRotationAngle == 90)
            {
                flipCode = 1;
            }
            if (webCamTexture.videoRotationAngle == 180)
            {
                flipCode = 0;
            }
            else if (webCamTexture.videoRotationAngle == 270)
            {
                flipCode = 0;
            }
        }
        else
        {
            if (webCamTexture.videoRotationAngle == 180)
            {
                flipCode = -1;
            }
            else if (webCamTexture.videoRotationAngle == 270)
            {
                flipCode = -1;
            }
        }

        if (flipCode > int.MinValue)
        {
            if (flipCode == 0)
            {
                FlipVertical(colors, colors, width, height);
            }
            else if (flipCode == 1)
            {
                FlipHorizontal(colors, colors, width, height);
            }
            else if (flipCode < 0)
            {
                Rotate180(colors, colors, height, width);
            }
        }
    }

    /// <summary>
    /// Flips vertical.
    /// </summary>
    /// <param name="src">Src colors.</param>
    /// <param name="dst">Dst colors.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    void FlipVertical<T>(IList<T> src, IList<T> dst, int width, int height)
    {
        for (var i = 0; i < height / 2; i++)
        {
            var y = i * width;
            var x = (height - i - 1) * width;
            for (var j = 0; j < width; j++)
            {
                int s = y + j;
                int t = x + j;
                var c = src[s];
                dst[s] = src[t];
                dst[t] = c;
            }
        }
    }

    /// <summary>
    /// Flips horizontal.
    /// </summary>
    /// <param name="src">Src colors.</param>
    /// <param name="dst">Dst colors.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    void FlipHorizontal<T>(IList<T> src, IList<T> dst, int width, int height)
    {
        for (int i = 0; i < height; i++)
        {
            int y = i * width;
            int x = y + width - 1;
            for (var j = 0; j < width / 2; j++)
            {
                int s = y + j;
                int t = x - j;
                var c = src[s];
                dst[s] = src[t];
                dst[t] = c;
            }
        }
    }

    /// <summary>
    /// Rotates 180 degrees.
    /// </summary>
    /// <param name="src">Src colors.</param>
    /// <param name="dst">Dst colors.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    void Rotate180<T>(IList<T> src, IList<T> dst, int height, int width)
    {
        int i = src.Count;
        for (int x = 0; x < i / 2; x++)
        {
            var t = src[x];
            dst[x] = src[i - x - 1];
            dst[i - x - 1] = t;
        }
    }
}


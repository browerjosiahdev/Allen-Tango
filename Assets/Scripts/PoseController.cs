using UnityEngine;
using System.Collections;
using Tango;
using System;
using UnityEngine.UI;

public class PoseController : MonoBehaviour, ITangoPose {
    public GameObject player;
    public GameObject plane;
    public float speed;
    public float jump;

    private TangoApplication m_tangoApplication;
    private Vector3 m_tangoPosition;
    private Quaternion m_tangoRotation;
    private Vector3 m_startPosition;

    private float m_movementScale = 3.0f;

    /// <summary>
    /// Initialize the class.
    /// </summary>
	void Start () {
        m_tangoRotation = Quaternion.identity;
        m_tangoPosition = Vector3.zero;
        m_startPosition = transform.position;
        m_tangoApplication = FindObjectOfType<TangoApplication>();

        if (m_tangoApplication != null)
        {
            m_tangoApplication.RegisterPermissionsCallback(PermissionsCallback);
            m_tangoApplication.RequestNecessaryPermissionsAndConnect();
            m_tangoApplication.Register(this);

            // Get a handle to the world.
            plane = GameObject.FindWithTag("World");

            // Apply the live texture from the web cam.
            WebCamTexture webCamTexture = new WebCamTexture();
            plane.GetComponent<Renderer>().material.mainTexture = webCamTexture;
            webCamTexture.Play();
        }
        else
        {
            Debug.Log("No Tango Manager found in scene.");
        }
	}

    /// <summary>
    /// Create a callback to receive permissions to use the motion tracking camera for Project Tango.
    /// </summary>
    /// <param name="success">Permission provided.</param>
    private void PermissionsCallback(bool success)
    {
        if (success)
        {
            m_tangoApplication.InitApplication();
            m_tangoApplication.InitProviders(string.Empty);
            m_tangoApplication.ConnectToService();
        }
        else
        {
            AndroidHelper.ShowAndroidToastMessage("Motion Tracking Permissions Needed", true);
        }
    }

    /// <summary>
    /// Pose callback from Project Tango.
    /// </summary>
    /// <param name="pose">Tango pose data.</param>
    public void OnTangoPoseAvailable(Tango.TangoPoseData pose)
    {
        if (pose == null)
        {
            Debug.Log("TangoPostData is null.");
            return;
        }
        
        if (pose.framePair.baseFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_START_OF_SERVICE && 
            pose.framePair.targetFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_DEVICE)
        {
            if (pose.status_code == TangoEnums.TangoPoseStatusType.TANGO_POSE_VALID)
            {
                m_tangoPosition = new Vector3((float)pose.translation[0],
                                              (float)pose.translation[1],
                                              (float)pose.translation[2]);

                m_tangoRotation = new Quaternion((float)pose.orientation[0],
                                                 (float)pose.orientation[1],
                                                 (float)pose.orientation[2],
                                                 (float)pose.orientation[3]);
            }
            else
            {
                m_tangoPosition = Vector3.zero;
                m_tangoRotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// Transform the Tango pose which is in Start of Services to Device from to Unity coordinate system.
    /// </summary>
    /// <param name="translation">Translation.</param>
    /// <param name="rotation">Rotation.</param>
    /// <param name="scale">Scale.</param>
    /// <returns>The Tango Pose in Unity coordinate system.</returns>
    Matrix4x4 TransformTangoPoseToUnityCoordinateSystem(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        Matrix4x4 uwTss;
        Matrix4x4 dTuc;

        uwTss = new Matrix4x4();
        uwTss.SetColumn(0, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
        uwTss.SetColumn(1, new Vector4(0.0f, 0.0f, 1.0f, 0.0f));
        uwTss.SetColumn(2, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
        uwTss.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

        dTuc = new Matrix4x4();
        dTuc.SetColumn(0, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
        dTuc.SetColumn(1, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
        dTuc.SetColumn(2, new Vector4(0.0f, 0.0f, -1.0f, 0.0f));
        dTuc.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

        Matrix4x4 ssTd = Matrix4x4.TRS(translation, rotation, scale);

        return uwTss * ssTd * dTuc;
    }

    /// <summary>
    /// Called everytime the frame updates.
    /// </summary>
    void Update()
    {
        // Transform the tango position into the Unity coordinate system.
        Matrix4x4 uwTuc = TransformTangoPoseToUnityCoordinateSystem(m_tangoPosition, m_tangoRotation, Vector3.one);

        // Extract new local position.
        transform.position = (uwTuc.GetColumn(3)) * m_movementScale;
        transform.position += m_startPosition;

        if (player != null)
        {
            Rigidbody playerBody = player.GetComponent<Rigidbody>();

            if (playerBody != null)
            {
                Vector3 force = Vector3.zero;
                Touch touch = new Touch();

                if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
                {
                    // Get the delta of the touch movement.
                    Vector2 touchDeltaPosition = Input.GetTouch(0).deltaPosition;

                    // Apply the changes to force object, and change the y to z.
                    force.x = touchDeltaPosition.x;
                    force.z = touchDeltaPosition.y;

                    // Transform the force based off the camera direction.
                    force = gameObject.transform.TransformDirection(force);
                }
                else if (touch.tapCount == 1)
                {
                    force.y = jump;

                    Debug.Log("JUMP!");
                }

                // Apply the force to the player body.
                playerBody.AddForce(force * speed);
            }
        }

        // Extract new local rotation.
        transform.rotation = Quaternion.LookRotation(uwTuc.GetColumn(2), uwTuc.GetColumn(1));
    }
}

using UnityEngine;
using System.Collections;
using System.IO;  // for writing files
using System.Collections.Generic;  //using "List"

public class jointsRotate : MonoBehaviour {

	GameObject joint1, joint2, joint3, joint4, joint5, joint6, endEff, gripperR, gripperL;
	GameObject joint1_a, joint2_a, joint3_a, joint4_a, joint5_a, joint6_a, endEff_a, gripperR_a, gripperL_a;  // 連線時,機械手臂實際姿態(角度)
	public int ctrl_trig_flag;
	public bool ctrl_menu_save_flag;  // to save points
	public Transform target_noap; 

	GameObject Robot_a;
	Client Robot_jRa;

	GameObject Grip_L, Grip_R;
	GripCollide GC_L, GC_R;  //Gripper_actual*2 Collide detect script

	//Parameter.
	float a1 = 10;    //100mm;
	float a2 = 25;    //250mm;
	float a3 = 13;    //130mm;
	float d1 = 35;    //350mm;
	float d4 = 25;    //250mm;
	float d6 = 17.9f;  //+19.675f;    //85mm + end-effector(94mm  //ignore: +34-26/2=115mm !??) => all:200mm;  =>學姊設95.5mm!!!

	///[HideInInspector] // Hides var below: or the initialization is invalid!!

	public float[] theta_tar;  //target theta
	float[] theta_now;  //present theta(now)
	float[] dtheta;  //delta theta(diff)

	float[] theta_now_a;  //VR robot的 present theta(now) 虛擬手臂旋轉用
	float[] dtheta_a;  //delta theta(diff) VR robot專用!!(跟jointsRotate不同喔!)

	//float[] ctrl_to_eff;  //Difference between Vive controller & Eff, for compensation (Concept: Adding one more joints!?)
	//Vector3 ctrl_0 = new Vector3(0, 73, -53);  //Initial position of the Vive controller when the trigger is pressed.
	Vector3 ctrl_rel_pos = new Vector3(0, 0, 0);  //relative position vector between end-effector & Vive controller
	Quaternion ctrl_rel_ori;  //relative orientation between end-effector & Vive controller
	public bool inRange = true;
	float[] max = new float[] { 155, 130, 25, 130, 115, 170 };  //Upper limit of each joint
	float[] min = new float[] { -155, 0, -75, -160, -100, -90 };  //Lower limit of each joint

	int GripCntDown, GripSgn, GripCntTmp;
	public GameObject TarCube1, TarCube2;  // Is it TarCube1 or 2

	// save points.
	List<string> sp_List = new List<string>();    //"SavePoint_virtual" List of string: "@time#theta1,theta2,...,theta6,theta7(gripper) $ctrl_trig_flag,ctrl_grip_flag,ctrl_tpad_flag"
	List<string> sp_List_a = new List<string>();  //"SavePoint_actual" List of string: "@time#theta1,theta2,...,theta6,theta7(gripper) $ctrl_trig_flag,ctrl_grip_flag,ctrl_tpad_flag"

	void Awake()  
	{   
		joint1 = GameObject.Find("Joint1");
		joint2 = GameObject.Find("Joint2");
		joint3 = GameObject.Find("Joint3");
		joint4 = GameObject.Find("Joint4");
		joint5 = GameObject.Find("Joint5");
		joint6 = GameObject.Find("Joint6");
		endEff = GameObject.Find("end_effector");
		gripperR = GameObject.Find ("EndEff_Gripper_Right");
		gripperL = GameObject.Find ("EndEff_Gripper_Left");

		joint1_a = GameObject.Find("Joint1_actual");
		joint2_a = GameObject.Find("Joint2_actual");
		joint3_a = GameObject.Find("Joint3_actual");
		joint4_a = GameObject.Find("Joint4_actual");
		joint5_a = GameObject.Find("Joint5_actual");
		joint6_a = GameObject.Find("Joint6_actual");
		endEff_a = GameObject.Find("end_effector_actual");
		gripperR_a = GameObject.Find ("EndEff_Gripper_Right_actual");
		gripperL_a = GameObject.Find ("EndEff_Gripper_Left_actual");
	
		Robot_a = GameObject.Find("RV_2A_actual");
		Robot_jRa = Robot_a.GetComponent<Client>();

		Grip_L = GameObject.Find ("Grip_L");
		Grip_R = GameObject.Find ("Grip_R");
		GC_L = Grip_L.GetComponent<GripCollide> ();
		GC_R = Grip_R.GetComponent<GripCollide> ();

		TarCube1 = GameObject.Find ("TarCube1");
		TarCube2 = GameObject.Find ("TarCube2");
	}

	// Use this for initialization
	void Start () {
		theta_tar = new float[] { 0, 90, 0, 0, 0, 0, 0 };	//[Caution!] the public var should initialize here!!!  ; Gripper: theta_now[6], 1: close; 0:open
		theta_now = new float[] { 0, 90, 0, 0, 0, 0, 0 };   //present theta(now)
		dtheta = new float[] { 0, 0, 0, 0, 0, 0, 0 };		//delta theta(diff)

		theta_now_a = new float[] { 0, 90, 0, 0, 0, 0, 0 }; //VR robot的 present theta(now) 虛擬手臂旋轉用
		dtheta_a = new float[] { 0, 0, 0, 0, 0, 0, 0 };	    //delta theta(diff) VR robot專用!!(跟jointsRotate不同喔!)

		ctrl_trig_flag = -1;
		ctrl_menu_save_flag = false;

		GripCntDown = 0;
		GripSgn = 0;
		GripCntTmp = 0;
		///int desiredSamplingRate = 30;
		///Time.fixedDeltaTime = 1/desiredSamplingRate;
	}

	// Update is called once per frame
	void Update () {

		if (ctrl_trig_flag == 0) {  // 紀錄壓下trigger的相對數值
			ctrl_rel_pos = target_noap.position - endEff.transform.position;
			//Debug.Log ("ctrl_rel_pos: " + (-ctrl_rel_pos.z).ToString () + ", " + (ctrl_rel_pos.x).ToString () + ", " + (ctrl_rel_pos.y).ToString ());
			ctrl_rel_ori = Quaternion.Inverse (target_noap.rotation) * endEff.transform.rotation;
			ctrl_trig_flag++;
		} else if (ctrl_trig_flag > 0) {
			j123456_IK (target_noap);
			CheckLimit ();  // 'return false' if out of range.(& bound limit angle & change color)
		
			//----- Revise Joint6 ----- 0509 Following "Don't USE !!!"
			// *** THIS IS EXP4_3 Pick & Place ***
			if (!inRange)
			{
				if ((theta_tar [5] > max [5])) {
					theta_tar [5] = theta_tar [5] - 180;
					CheckLimit (); 
				} else if (theta_tar [5] < min [5]) {
					theta_tar [5] = theta_tar [5] + 180;
					CheckLimit (); 
				}
			}

		
		
		}

		//***** VR_robot rotate *****
		for (int i = 0; i < 7; i++)
			dtheta [i] = theta_tar [i] - theta_now [i];

		joint1.transform.RotateAround (Vector3.zero, Vector3.up, dtheta [0]);
		joint2.transform.Rotate (Vector3.up, dtheta [1]);
		joint3.transform.Rotate (Vector3.up, dtheta [2]);
		joint4.transform.Rotate (Vector3.up, dtheta [3]);
		joint5.transform.Rotate (Vector3.up, dtheta [4]);
		joint6.transform.Rotate (Vector3.up, dtheta [5]);

		//gripperR.transform.Translate (0, 0, -3 * dtheta [6]);  //夾的速度可再調
		//gripperL.transform.Translate (0, 0, 3 * dtheta [6]);

		// *** Gripper Collision Detect ***
		/* <IN GC_L, GC_R>
		 * bool CollideFlag = false;
		 * //int noCollideCnt;
		*/
			
		// Still have problem... 0510  //OK!!?
		if (dtheta [6] != 0) {
			if (GripCntTmp != 0) {
				GripCntDown = GripCntTmp;
				GripCntTmp = 0;
			} else
				GripCntDown = 100;  //initial default

			GripSgn = (int)dtheta [6];
		}

		if(GripCntDown > 0){
			if ((GC_L.CollideFlag) & (GC_R.CollideFlag)) {
				GripCntTmp = 100 - GripCntDown;
				GripCntDown = 0;

				if (GC_L.CubeNo == GC_R.CubeNo) {
					// this is Not smart...
					if(GC_L.CubeNo==1)
						TarCube1.GetComponent<GripMove>().enabled = true;
						//TarCube1.transform.parent = endEff.transform;
					else if(GC_L.CubeNo==2)
						TarCube2.GetComponent<GripMove>().enabled = true;
						///TarCube2.transform.parent = endEff.transform;



					Debug.Log ("CubeNo"+ GC_L.CubeNo);
				} else
					Debug.Log ("ERROR: CubeNumber not consistent!");
				//GC_L.CollideFlag = false;
				//GC_R.CollideFlag = false;
				//Debug.Log ("Both Collide");

				//Debug.Log("GripObject : " + GripObject.name);
				//GripObject.GetComponent<Rigidbody>().useGravity = false;
				//GripObject.GetComponent<Rigidbody>().isKinematic = true;
				///GripObject.transform.parent = endEff.transform;
			}
			else {
				gripperR.transform.Translate (0, 0, -0.03f * GripSgn);
				gripperL.transform.Translate (0, 0, 0.03f * GripSgn);
			}

		}

		GripCntDown--;
		if (GripCntDown < 0) {
			if ((GC_L.CollideFlag) & (GC_R.CollideFlag)) {
			}
			GC_L.CollideFlag = false;
			GC_R.CollideFlag = false;
			GripCntDown = 0;

			//TarCube1.transform.parent = null;
			//TarCube2.transform.parent = null;
			if (GC_L.CubeNo == GC_R.CubeNo) {
				if(GC_L.CubeNo==1)
					TarCube1.GetComponent<GripMove>().enabled = false;
				//TarCube1.transform.parent = endEff.transform;
				else if(GC_L.CubeNo==2)
					TarCube2.GetComponent<GripMove>().enabled = false;
				///TarCube2.transform.parent = endEff.transform;
				GC_L.CubeNo = 0;
				GC_R.CubeNo = 0;
			}
		}

		for (int i = 0; i < 7; i++)		//refresh: after rotate
			theta_now [i] = theta_tar [i];
			

		//***** VR_robot_actual rotate *****
		for (int i = 0; i < 7; i++)
			dtheta_a [i] = Robot_jRa.theta_tar_a [i] - theta_now_a [i];

		joint1_a.transform.RotateAround (Vector3.zero, Vector3.up, dtheta_a [0]);
		joint2_a.transform.Rotate (Vector3.up, dtheta_a [1]);
		joint3_a.transform.Rotate (Vector3.up, dtheta_a [2]);
		joint4_a.transform.Rotate (Vector3.up, dtheta_a [3]);
		joint5_a.transform.Rotate (Vector3.up, dtheta_a [4]);
		joint6_a.transform.Rotate (Vector3.up, dtheta_a [5]);

		gripperR_a.transform.Translate (0, 0, -3 * dtheta_a [6]);  //夾的速度可再調
		gripperL_a.transform.Translate (0, 0, 3 * dtheta_a [6]);

		//Debug.Log ("Robot_jRa.theta_tar_a[6]: " + Robot_jRa.theta_tar_a [6].ToString ());

		for (int i = 0; i < 7; i++)		//refresh: after rotate
			theta_now_a [i] = Robot_jRa.theta_tar_a [i];

		//***** save point (virtual) *****
		string save_str = ctrl_trig_flag.ToString() + "@" + Time.time.ToString("f4") + "#" + theta_now[0].ToString("f4") + "," + theta_now[1].ToString("f4") + "," + theta_now[2].ToString("f4") + "," + theta_now[3].ToString("f4") + ","+ theta_now[4].ToString("f4") + "," + theta_now[5].ToString("f4") + "," + theta_tar[6].ToString("f4") + "$" + Robot_jRa.ctrl_grip_flag.ToString() + "," + Robot_jRa.ctrl_tpad_flag.ToString();
		sp_List.Add(save_str);

		//***** save point (actual) *****
		string save_str_a = "@" + Time.time.ToString("f4") + "#" + theta_now_a[0].ToString("f4") + "," + theta_now_a[1].ToString("f4") + "," + theta_now_a[2].ToString("f4") + "," + theta_now_a[3].ToString("f4") + ","+ theta_now_a[4].ToString("f4") + "," + theta_now_a[5].ToString("f4") + "," + Robot_jRa.theta_tar_a[6].ToString("f4");
		sp_List_a.Add(save_str_a);

		if (Input.GetKeyDown("space")||(ctrl_menu_save_flag))
		{
			save2file(sp_List, Time.time.ToString("f4")+"_v");
			save2file(sp_List_a, Time.time.ToString("f4")+"_a");
			Debug.Log ("save&print!");
			ctrl_menu_save_flag = false;
		}

	}

	void j123_IK(Transform tar)
	{
		/*float pwx = -tar.position.z;
		float pwy = tar.position.x;
		float pwz = tar.position.y;*/

		//****************************************** tar_Eff

		float ax6 = Vector3.Dot(endEff.transform.up, Vector3.back);		// ax = (endEff_z = y in unity) dot (world_x = -z in unity)
		float ay6 = Vector3.Dot (endEff.transform.up, Vector3.right);	// ay = (endEff_z = y in unity) dot (world_y =  x in unity)
		float az6 = Vector3.Dot(endEff.transform.up, Vector3.up);		// az = (endEff_z = y in unity) dot (world_z =  y in unity)

		float pwx, pwy, pwz;
		float px = -tar.position.z;
		float py = tar.position.x;
		float pz = tar.position.y;

		pwx = px - d6 * ax6;
		pwy = py - d6 * ay6;
		pwz = pz - d6 * az6;
		//******************************************

		//theta1. -1
		theta_tar [0] = Mathf.Atan2 (pwy, pwx) ;
		//theta_tar [0] = Mathf.Atan2 (pwy, pwx) + 180 * Mathf.Deg2Rad;
		//Debug.Log ("j1 - "+theta_tar [0]);

		float p1wx, p1wy, p1wz;
		p1wx = Mathf.Cos (theta_tar [0]) * pwx + Mathf.Sin (theta_tar [0]) * pwy - a1;
		p1wy = -pwz + d1;
		p1wz = -Mathf.Sin (theta_tar [0]) * pwx + Mathf.Cos (theta_tar [0]) * pwy;

		float phi = Mathf.Atan2 (p1wy, p1wx);
		float r = Mathf.Sqrt (p1wx * p1wx + p1wy * p1wy);
		float l = Mathf.Sqrt (a3 * a3 + d4 * d4);
		float gamma = Mathf.Atan2 (d4, a3);

		float alpha, beta;
		//theta2. & 3. -1
		if (r > l + a2) {
			theta_tar [1] = phi;
			theta_tar [2] = -gamma;
		} else {
			alpha = Mathf.Acos ((r * r + a2 * a2 - l * l) / (2 * r * a2));
			beta = Mathf.Acos ((r * r - a2 * a2 + l * l) / (2 * r * l));
			theta_tar [1] = phi - alpha;
			theta_tar [2] = alpha + beta - gamma;
		}

		//!!! unity y-axis => -y  &&  Rad => Deg  
		for (int i = 0; i < 6; i++)
			theta_tar [i] = -theta_tar [i] * Mathf.Rad2Deg;
		
	}

	void j123456_IK(Transform tar)
	{
		//---Old Version---//
		/*float px = -tar.position.z;
		float py = tar.position.x;
		float pz = tar.position.y;

		float nx = Vector3.Dot (-tar.forward, Vector3.back);	// nx = (endEff_x = -z in unity) dot (world_x = -z in unity)
		float ny = Vector3.Dot (-tar.forward, Vector3.right);	// ny = (endEff_x = -z in unity) dot (world_y =  x in unity)
		float nz = Vector3.Dot (-tar.forward, Vector3.up);		// nz = (endEff_x = -z in unity) dot (world_z =  y in unity)
		float ox = Vector3.Dot (tar.right, Vector3.back);		// ox = (endEff_y = x in unity) dot (world_x = -z in unity)
		float oy = Vector3.Dot (tar.right, Vector3.right);		// oy = (endEff_y = x in unity) dot (world_y =  x in unity)
		float oz = Vector3.Dot (tar.right, Vector3.up);			// oz = (endEff_y = x in unity) dot (world_z =  y in unity)
		float ax = Vector3.Dot (tar.up, Vector3.back);			// ax = (endEff_z = y in unity) dot (world_x = -z in unity)
		float ay = Vector3.Dot (tar.up, Vector3.right);			// ay = (endEff_z = y in unity) dot (world_y =  x in unity)
		float az = Vector3.Dot (tar.up, Vector3.up);			// az = (endEff_z = y in unity) dot (world_z =  y in unity)
		*/

		float px = -tar.position.z + ctrl_rel_pos.z;
		float py = tar.position.x - ctrl_rel_pos.x;
		float pz = tar.position.y - ctrl_rel_pos.y;

		Quaternion tar_new = tar.rotation * ctrl_rel_ori;
		float nx = Vector3.Dot (-(tar_new * Vector3.forward), Vector3.back);		// nx = (endEff_x = -z in unity) dot (world_x = -z in unity)
		float ny = Vector3.Dot (-(tar_new * Vector3.forward), Vector3.right);		// ny = (endEff_x = -z in unity) dot (world_y =  x in unity)
		float nz = Vector3.Dot (-(tar_new * Vector3.forward), Vector3.up);		// nz = (endEff_x = -z in unity) dot (world_z =  y in unity)
		float ox = Vector3.Dot (tar_new * Vector3.right, Vector3.back);			// ox = (endEff_y = x in unity) dot (world_x = -z in unity)
		float oy = Vector3.Dot (tar_new * Vector3.right, Vector3.right);		// oy = (endEff_y = x in unity) dot (world_y =  x in unity)
		float oz = Vector3.Dot (tar_new * Vector3.right, Vector3.up);			// oz = (endEff_y = x in unity) dot (world_z =  y in unity)
		float ax = Vector3.Dot (tar_new * Vector3.up, Vector3.back);			// ax = (endEff_z = y in unity) dot (world_x = -z in unity)
		float ay = Vector3.Dot (tar_new * Vector3.up, Vector3.right);			// ay = (endEff_z = y in unity) dot (world_y =  x in unity)
		float az = Vector3.Dot (tar_new * Vector3.up, Vector3.up);				// az = (endEff_z = y in unity) dot (world_z =  y in unity)

		float pwx, pwy, pwz;
		pwx = px - d6 * ax;
		pwy = py - d6 * ay;
		pwz = pz - d6 * az;

		//***theta1. -1
		theta_tar [0] = Mathf.Atan2 (pwy, pwx) ;
		//theta_tar [0] = Mathf.Atan2 (pwy, pwx) + 180 * Mathf.Deg2Rad;

		float p1wx, p1wy, p1wz;
		p1wx = Mathf.Cos (theta_tar [0]) * pwx + Mathf.Sin (theta_tar [0]) * pwy - a1;
		p1wy = -pwz + d1;
		p1wz = -Mathf.Sin (theta_tar [0]) * pwx + Mathf.Cos (theta_tar [0]) * pwy;

		float phi = Mathf.Atan2 (p1wy, p1wx);
		float r = Mathf.Sqrt (p1wx * p1wx + p1wy * p1wy);
		float l = Mathf.Sqrt (a3 * a3 + d4 * d4);
		float gamma = Mathf.Atan2 (d4, a3);

		float alpha, beta;
		//***theta2 
		if (r > l + a2) {
			theta_tar [1] = phi;
			theta_tar [2] = -gamma;
		} else {
			alpha = Mathf.Acos ((r * r + a2 * a2 - l * l) / (2 * r * a2));
			beta = Mathf.Acos ((r * r - a2 * a2 + l * l) / (2 * r * l));
			theta_tar [1] = phi - alpha ;
			theta_tar [2] = alpha + beta - gamma;
		}

		//***theta4. & 5. & 6. -1
		float n03x, n03y, n03z, o03x, o03y, o03z, a03x, a03y, a03z;
		float n3z, o3z, a3x, a3y, a3z;

		n03x = Mathf.Cos (theta_tar [0]) * Mathf.Cos (theta_tar [1] + theta_tar [2]);
		n03y = Mathf.Sin (theta_tar [0]) * Mathf.Cos (theta_tar [1] + theta_tar [2]);
		n03z = -Mathf.Sin (theta_tar [1] + theta_tar [2]);
		o03x = Mathf.Sin (theta_tar [0]);
		o03y = -Mathf.Cos (theta_tar [0]);
		o03z = 0;
		a03x = -Mathf.Cos (theta_tar [0]) * Mathf.Sin (theta_tar [1] + theta_tar [2]);
		a03y = -Mathf.Sin (theta_tar [0]) * Mathf.Sin (theta_tar [1] + theta_tar [2]);
		a03z = -Mathf.Cos (theta_tar [1] + theta_tar [2]);

		//n3x = n03x * nx + n03y * ny + n03z * nz;
		//n3y = o03x * nx + o03y * ny + o03z * nz;
		n3z = a03x * nx + a03y * ny + a03z * nz;
		//o3x = n03x * ox + n03y * oy + n03z * oz;
		//o3y = o03x * ox + o03y * oy + o03z * oz;
		o3z = a03x * ox + a03y * oy + a03z * oz;
		a3x = n03x * ax + n03y * ay + n03z * az;
		a3y = o03x * ax + o03y * ay + o03z * az;
		a3z = a03x * ax + a03y * ay + a03z * az;

		// theta456 -1
		theta_tar [3] = Mathf.Atan2 (-a3y, -a3x) ;
		theta_tar [4] = Mathf.Atan2 (Mathf.Sqrt (a3x * a3x + a3y * a3y), a3z);
		theta_tar [5] = Mathf.Atan2 (-o3z, n3z) ;

		if (theta_tar [4] < 0) {
			// theta456 -2
			theta_tar [3] = Mathf.Atan2 (a3y, a3x) ;
			theta_tar [4] = Mathf.Atan2 (-Mathf.Sqrt (a3x * a3x + a3y * a3y), a3z) - 90 * Mathf.Deg2Rad;
			theta_tar [5] = Mathf.Atan2 (o3z, -n3z);
		}

		//!!! unity y-axis => -y  &&  Rad => Deg  
		for (int i = 0; i < 6; i++)
			theta_tar [i] = -theta_tar [i] * Mathf.Rad2Deg;
	}
		
	void CheckLimit(){  //Check joints Limit(bounded angle) & Change Color(if it is out of range. -> 'return false') 

		inRange = true;  //inRange = false if it is out of range (=> retrun false and don't rotate) 

		//*** Upper & Lower limit of 6 joints *** //!!! Cautious: axis in Unity is opposite...(w/ minus sign, so 'max' is the Lower limit; 'min' is the Upper limit)
		// 須先轉成Unity座標(除了正負號,還有初始角度等..)
		///float[] max = new float[] { 155, 130, 25, 130, 115, 170 };  //Upper limit of each joint
		///float[] min = new float[] { -155, 0, -75, -160, -100, -90 };  //Lower limit of each joint
		string[] Joint_tag = new string[]{"J1","J2","J3","J4","J5","J6"};  //right now(20170301) there is no J6 ...

		// !!! Have problem: if can't achieve, it still might exist other solutio	n set(joint angle) that is closer than this.(bounded one joints)
		for (int i = 0; i < 6; i++) {

			GameObject[] changejoint = GameObject.FindGameObjectsWithTag (Joint_tag [i]);

			if (theta_tar [i] > max [i] || theta_tar [i] < min [i]) {
				// ***** Change Color *****
				for (int j = 0; j < changejoint.Length; j++)
					changejoint [j].GetComponent<Renderer> ().material = (Material)Resources.Load ("black");  //Find out RV2A_CAD that belong to the respective joint.
				inRange = false;
			} else {
				for (int j = 0; j < changejoint.Length; j++)
					changejoint [j].GetComponent<Renderer> ().material = (Material)Resources.Load ("original_RV2A");  //in range: Change back to original color
			}
		}
	}


	void save2file(List<string> list2print, string nameStr)
	{	
		string fileName = nameStr + ".txt";
		FileStream fs = null;

		try
		{
			fs = new FileStream(fileName, FileMode.OpenOrCreate);
			StreamWriter sw = new StreamWriter(fs);

			for (int i = 0; i < list2print.Count; i++) {  //for each!?
				sw.WriteLine(list2print[i]);
			}

			sw.Close();
		} 
		catch (IOException ex)
		{
			return ;
		}
	}

}
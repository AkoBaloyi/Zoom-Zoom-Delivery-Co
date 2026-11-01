using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using ZoomZoom.Orders;

namespace ZoomZoom.Vehicle.Editor
{
    public static partial class CrazyTaxiValidation
    {
        private static void TestRoadRecovery()
        {
            var timerObject = new GameObject("Recovery timer test");
            var timer = timerObject.AddComponent<ShiftTimer>();
            timer.StartShift();
            float initialTime = timer.TimeRemaining;
            ResetCar();
            Vector3 safe = _car.transform.position;
            int count = _car.SelfRightCount;
            // Move the car without invoking its deliberate teleport/reset API, preserving its
            // last clear road checkpoint as a real collision would.
            _car.Body.position = new Vector3(8f, 0.55f, 0f);
            _car.Body.rotation = Quaternion.Euler(0f, 0f, 180f);
            _car.transform.SetPositionAndRotation(_car.Body.position, _car.Body.rotation);
            Tick(2.85f);
            Check(_car.SelfRightCount == count, "Overturned recovery waits three seconds");
            Tick(0.4f);
            Check(_car.SelfRightCount == count + 1 && _car.Uprightness > 0.9f &&
                Vector3.Distance(_car.transform.position, safe) < 2f,
                "Overturned car returns to its last clear supported road point");
            Check(Mathf.Abs(timer.TimeRemaining - (initialTime - _car.Taxi.recoveryTimeCost)) < 0.01f,
                "Successful recovery deducts the configured five seconds from the active shift");

            ResetCar();
            count = _car.SelfRightCount;
            Tick(3.5f);
            Check(_car.SelfRightCount == count, "Parked upright car never auto-resets");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(8f, 1.5f, 2.5f);
            wall.transform.localScale = new Vector3(8f, 3f, 0.5f);
            _car.Body.position = new Vector3(8f, 0.55f, 0f);
            _car.transform.position = _car.Body.position;
            _car.Drive = new DriveInput { throttle = 1f };
            Tick(3.6f);
            Check(_car.SelfRightCount == count + 1 && _car.transform.position.x < 2f,
                $"Upright car held against a wall for three seconds returns to clear road (resets {_car.SelfRightCount - count}, position {_car.transform.position}, speed {_car.Speed:0.00})");
            float after = timer.TimeRemaining;
            Tick(3.5f);
            Check(timer.TimeRemaining == after, "Recovery clears drive input and charges once, not every frame");
            timer.ApplyTimePenalty(10000f);
            Check(timer.TimeRemaining == 0f && !timer.IsShiftActive, "Recovery time cost can end a shift without negative time");
            UnityEngine.Object.DestroyImmediate(wall);
            UnityEngine.Object.DestroyImmediate(timerObject);
            ResetCar();
        }

        private static void MeasureLevelDesign()
        {
            var rows = new List<string> { "metric,value,unit,condition" };
            ResetCar();
            _car.Drive = new DriveInput { throttle = 1f };
            float time99 = -1f, time100 = -1f, maximum = 0f;
            for (int frame = 1; frame <= 12 * 120; frame++)
            {
                Tick(Dt);
                maximum = Mathf.Max(maximum, _car.ForwardSpeed);
                if (time99 < 0f && _car.ForwardSpeed >= _car.Taxi.cruiseSpeed * 0.99f) time99 = frame * Dt;
                if (time100 < 0f && _car.ForwardSpeed >= 100f / 3.6f) time100 = frame * Dt;
            }
            rows.Add(Row("normal_top_speed", maximum * 3.6f, "km/h", "12 seconds full throttle; no tricks"));
            rows.Add(Row("zero_to_99_percent_top_speed", time99, "s", "99 percent of 28 m/s; exact cap is asymptotic"));
            rows.Add(Row("zero_to_100_kmh", time100, "s", "no trick; all automatic shifts included"));
            Vector3 brakingStart = _car.transform.position;
            _car.Drive = new DriveInput { brake = 1f };
            float stopTime = 0f;
            while (stopTime < 5f && _car.Speed > 0.1f) { Tick(Dt); stopTime += Dt; }
            float stopDistance = Vector3.ProjectOnPlane(_car.transform.position - brakingStart, Vector3.up).magnitude;
            rows.Add(Row("top_speed_to_stop", stopTime, "s", "full brake; stopped below 0.1 m/s"));
            rows.Add(Row("top_speed_stopping_distance", stopDistance, "m", "full brake; dry flat tarmac"));
            Check(time99 > 0f && maximum <= _car.Taxi.cruiseSpeed + 0.1f && stopTime < 2f,
                "Level design straight-line measurements reach cruise and stop within two seconds");

            float tightest = float.PositiveInfinity, sweptDiameter = float.PositiveInfinity, circleSpeed = 0f;
            foreach (float speed in new[] { 1f, 2f, 3f, 4f, 5f, 8f })
            {
                ResetCar();
                _car.Body.linearVelocity = Vector3.forward * speed;
                _car.Drive = new DriveInput { steer = 1f, throttle = 0.01f };
                float sum = 0f, sweptSum = 0f;
                int samples = 0;
                for (int frame = 0; frame < 4 * 120; frame++)
                {
                    // Hold a repeatable low speed with a proportional pedal; steering remains
                    // entirely the production controller, with no forced pose or yaw.
                    _car.Drive.throttle = Mathf.Clamp01((speed - _car.ForwardSpeed) * 0.5f);
                    Tick(Dt);
                    if (frame > 2 * 120 && Mathf.Abs(_car.YawRate) > 0.05f)
                    {
                        Vector3 velocity = Vector3.ProjectOnPlane(_car.Body.linearVelocity, Vector3.up);
                        sum += velocity.magnitude / Mathf.Abs(_car.YawRate);
                        Vector3 centre = _car.transform.position + Vector3.Cross(Vector3.up, velocity) / _car.YawRate;
                        float outer = 0f;
                        BoxCollider box = _car.GetComponent<BoxCollider>();
                        foreach (float x in new[] { -box.size.x * 0.5f, box.size.x * 0.5f })
                            foreach (float z in new[] { -box.size.z * 0.5f, box.size.z * 0.5f })
                                outer = Mathf.Max(outer, Vector3.ProjectOnPlane(
                                    _car.transform.TransformPoint(box.center + new Vector3(x, 0f, z)) - centre, Vector3.up).magnitude);
                        sweptSum += outer * 2f;
                        samples++;
                    }
                }
                float radius = samples > 0 ? sum / samples : float.PositiveInfinity;
                rows.Add(Row("turn_radius_at_" + speed.ToString("0", CultureInfo.InvariantCulture) + "_mps",
                    radius, "m", "steady full lock; mean actual speed / yaw rate over last 2 seconds"));
                if (radius < tightest) { tightest = radius; circleSpeed = speed; }
                if (samples > 0) sweptDiameter = Mathf.Min(sweptDiameter, sweptSum / samples);
            }
            rows.Add(Row("tightest_center_path_turning_diameter", tightest * 2f, "m", "lowest of tested 1 2 3 4 5 8 m/s"));
            rows.Add(Row("tightest_whole_car_swept_turning_diameter", sweptDiameter, "m", "includes outer corners of production Car prefab collider"));
            rows.Add(Row("tightest_circle_target_speed", circleSpeed * 3.6f, "km/h", "steady full lock"));

            float fastest = 0f, fastestExit = 0f;
            var corners = new List<string> { "entry_kmh,exit_kmh,inside_12m_road,heading_degrees,max_roll_degrees" };
            for (int sample = 30; sample <= 101; sample++)
            {
                float kmh = Mathf.Min(sample, _car.Taxi.cruiseSpeed * 3.6f);
                ResetCar();
                _car.Body.linearVelocity = Vector3.forward * (kmh / 3.6f);
                _car.Drive = new DriveInput { steer = 1f };
                bool inside = true;
                float roll = 0f, heading = 0f;
                for (int frame = 0; frame < 3 * 120; frame++)
                {
                    heading = Vector3.SignedAngle(Vector3.forward, _car.transform.forward, Vector3.up);
                    _car.Drive.steer = heading < 82f ? 1f : 0f;
                    Tick(Dt);
                    roll = Mathf.Max(roll, Vector3.Angle(_car.transform.up, Vector3.up));
                    inside &= CornerContainsCar();
                    if (heading >= 85f && Mathf.Abs(_car.YawRate) < 0.1f) break;
                }
                heading = Vector3.SignedAngle(Vector3.forward, _car.transform.forward, Vector3.up);
                bool passed = inside && heading >= 85f && heading <= 100f &&
                    _car.Speed * 3.6f >= kmh * 0.8f && roll < 10f;
                corners.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1:F2},{2},{3:F2},{4:F2}",
                    kmh, _car.Speed * 3.6f, passed, heading, roll));
                if (passed) { fastest = kmh; fastestExit = _car.Speed * 3.6f; }
            }
            rows.Add(Row("fastest_tested_90_degree_corner_entry", fastest, "km/h", "12m wide road; 6m inner and 18m outer radius; full lock then straighten; no tricks"));
            rows.Add(Row("fastest_tested_90_degree_corner_exit", fastestExit, "km/h", "whole production collider footprint on road; at least 80 percent speed retained; under 10 degrees roll"));
            Check(fastest > 0f && !float.IsInfinity(tightest), "Lab records a supported turning circle and a valid bounded 90-degree corner");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../VehicleLab_Measurements"));
            File.WriteAllLines(Path.Combine(directory, "level_design_numbers_2026-10-26.csv"), rows);
            File.WriteAllLines(Path.Combine(directory, "corner_speed_sweep_2026-10-26.csv"), corners);
            ResetCar();
        }

        private static string Row(string metric, float value, string unit, string condition) =>
            string.Format(CultureInfo.InvariantCulture, "{0},{1:F3},{2},{3}", metric, value, unit, condition);

        private static bool CornerContainsCar()
        {
            BoxCollider box = _car.GetComponent<BoxCollider>();
            foreach (float x in new[] { -box.size.x * 0.5f, box.size.x * 0.5f })
                foreach (float z in new[] { -box.size.z * 0.5f, box.size.z * 0.5f })
                {
                    Vector3 p = _car.transform.TransformPoint(box.center + new Vector3(x, 0f, z));
                    bool approach = p.z <= 0f && p.x >= -6f && p.x <= 6f;
                    bool exit = p.x >= 12f && p.z >= 6f && p.z <= 18f;
                    float radius = new Vector2(p.x - 12f, p.z).magnitude;
                    bool arc = p.x <= 12f && p.z >= 0f && radius >= 6f && radius <= 18f;
                    if (!approach && !exit && !arc) return false;
                }
            return true;
        }
    }
}

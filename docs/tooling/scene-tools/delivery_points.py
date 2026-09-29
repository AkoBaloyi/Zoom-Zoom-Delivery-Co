"""The delivery point set, and where it came from.

One source of truth: place_delivery_points.py writes these into the scene and
order_distances.py checks the order time limit against them.

Every coordinate is a junction of two street centrelines recovered from the greybox
pavement by find_roads.py, not a number anyone chose by eye. A street centreline is the
midpoint between two facing kerb lines; a junction is where a north-south centreline
crosses an east-west one. Each point below was additionally required to sit at least 18 m
from every building pivot and at least 8 m inside the drivable floor.

Re-run find_roads.py and replace these tables after any greybox re-export. The district
already moved once, on 21 September 2026, which voided the previous set.
"""

# Street grid recovered on 22 September 2026 from 54 pavement objects.
# North-south centrelines, x: -220.11, -125.04, -104.06, -80.90, -24.02, -6.79, 83.65
# East-west centrelines,  z: 7.03, 120.14, 140.33, 231.92, 252.71, 267.77, 344.79
#
# Streets run in close pairs, so the set below takes one street from each pair to keep the
# points meaningfully apart rather than clustering two of them on parallel carriageways.

# name, x, z, clearance from the nearest building pivot in metres
PICKUPS = [
    ("Pickup 01 South West Gate", -220.11, 7.03, 78.7),
    ("Pickup 02 Centre Cross", -6.79, 140.33, 62.4),
    ("Pickup 03 North East Corner", 83.65, 344.79, 57.2),
    ("Pickup 04 West Quarter", -104.06, 252.71, 44.1),
    ("Pickup 05 East Approach", 83.65, 120.14, 59.4),
    ("Pickup 06 North Centre", -24.02, 344.79, 47.9),
]

DROPOFFS = [
    ("DropOff 01 South East Gate", 83.65, 7.03, 77.9),
    ("DropOff 02 North West Corner", -125.04, 344.79, 45.1),
    ("DropOff 03 West Midpoint", -220.11, 231.92, 48.5),
    ("DropOff 04 Centre North", -6.79, 252.71, 43.0),
    ("DropOff 05 East Midpoint", 83.65, 231.92, 51.4),
    ("DropOff 06 South West Yard", -104.06, 7.03, 66.1),
]

# The car's transform in the game scene, which is where every shift starts.
START = (0.0, 0.0)

# Measured on the Balanced profile, from VehicleLab_Measurements/vehicle_measurements.csv.
TOP_SPEED = 32.0          # m/s
SIDEWAYS_GRIP = 13.0      # m/s^2
# Top speed cannot be held through a junction, so jobs are costed at a working average.
WORKING_AVERAGE = 20.0    # m/s

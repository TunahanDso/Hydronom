// Hydronom MCU - Vehicle Profile
//
// MİMARİ NOT:
// Pico tarafında araç fiziği, rota, görev veya 6DoF çözümü yapılmaz.
// Bu profil sadece "bu MCU şu araç rolünde kaç actuator çıkışı sunuyor"
// bilgisini taşır.
//
// Asıl karar Hydronom Runtime tarafındadır.

use super::actuator_profile::{ActuatorBankProfile, ActuatorProfile};

#[allow(dead_code)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VehicleFamily {
    SurfaceVessel,
    UnderwaterVehicle,
    UnderwaterRocket,
    AerialVehicle,
    GroundVehicle,
    Generic,
}

#[derive(Clone, Copy, Debug)]
pub struct VehicleProfile {
    pub family: VehicleFamily,
    pub actuator_bank: ActuatorBankProfile,
}

#[allow(dead_code)]
impl VehicleProfile {
    pub const fn generic_surface_4esc() -> Self {
        Self {
            family: VehicleFamily::SurfaceVessel,
            actuator_bank: ActuatorBankProfile {
                actuators: [
                    Some(ActuatorProfile::one_way_esc(0, 2)),
                    Some(ActuatorProfile::one_way_esc(1, 3)),
                    Some(ActuatorProfile::one_way_esc(2, 4)),
                    Some(ActuatorProfile::one_way_esc(3, 5)),
                    None,
                    None,
                    None,
                    None,
                ],
            },
        }
    }

    pub const fn generic_underwater_8esc_bidirectional() -> Self {
        Self {
            family: VehicleFamily::UnderwaterVehicle,
            actuator_bank: ActuatorBankProfile {
                actuators: [
                    Some(ActuatorProfile::bidirectional_esc(0, 2)),
                    Some(ActuatorProfile::bidirectional_esc(1, 3)),
                    Some(ActuatorProfile::bidirectional_esc(2, 4)),
                    Some(ActuatorProfile::bidirectional_esc(3, 5)),
                    Some(ActuatorProfile::bidirectional_esc(4, 6)),
                    Some(ActuatorProfile::bidirectional_esc(5, 7)),
                    Some(ActuatorProfile::bidirectional_esc(6, 8)),
                    Some(ActuatorProfile::bidirectional_esc(7, 9)),
                ],
            },
        }
    }
}
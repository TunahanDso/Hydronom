# Ana UUV Tek Parkur Diagnostik Notları

## Beklenen akış

1. main_001_start
2. main_010_line_entry
3. main_020_line_straight_1
4. main_030_line_corner
5. main_040_line_vertical_mid
6. main_050_line_turn_end
7. main_060_handoff_zone
8. main_070_autonomous_intikal_start
9. main_080_buoy_approach
10. main_090/main_100/main_110/main_120/main_130 orbit waypoints
11. main_140_finish_approach
12. main_150_enter_finish_square
13. main_160_surface_inside_finish

## Özellikle düzeltilen eski sorun

Eski full pakette farklı stage objeleri aynı anda aktif kalabildiği için line-following sırasında autonomous no-go zone geometry authority tarafından collision kabul edilebiliyordu.

Bu pakette:

- Tek stage var.
- No-go zone yok.
- Pipe görsel-only ve geometryBlocking=false.
- Bütün objelerde missionStage=advanced_main_uuv_single_course.
- Judge stageScope active_stage_only.
- Collision radius 0.24 m.
- Planner inflation 0.10 m.

## Testte beklenen log

- ADVANCE_BLOCKED_BY_GEOMETRY görülmemeli.
- nearest=no_go_zone_auto_1 görülmemeli.
- planRisk sürekli 1.00 kalmamalı.
- lf_020 civarında araç no-go collision'a girmemeli.
#!/bin/bash
# generator runs for extended + communication instructions in the open project (LAD, then FBD for extended)
cd "$(dirname "$0")/.."
declare -A P=( [extended]=ex [modbus]=mb [modbustcp]=mt [openuc]=ou [ptp]=pt [s7comm]=sc [uss]=us [webserver]=ws [teleservice]=ts )
for cat in extended modbus modbustcp openuc ptp s7comm uss webserver teleservice; do
  python tools/gen_lad.py LAD all --cat $cat --tag "${P[$cat]}" > docs/catalog/run_${cat}_lad.log 2>&1
done
python tools/gen_lad.py FBD all --cat extended --tag ex > docs/catalog/run_extended_fbd.log 2>&1
echo done > docs/catalog/run_ext.done

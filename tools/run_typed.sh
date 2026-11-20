#!/bin/bash
# typed generation for extended + communication catalogs (LAD) in the open project
cd "$(dirname "$0")/.."
declare -A P=( [extended]=ex [modbus]=mb [modbustcp]=mt [openuc]=ou [ptp]=pt [s7comm]=sc [uss]=us [webserver]=ws [teleservice]=ts )
for cat in extended openuc s7comm webserver modbus modbustcp ptp uss teleservice; do
  python tools/gen_typed.py LAD $cat all --tag "${P[$cat]}" > docs/catalog/typed_${cat}_lad.log 2>&1
done
echo done > docs/catalog/run_typed.done

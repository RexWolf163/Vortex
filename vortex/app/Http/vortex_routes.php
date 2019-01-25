<?php
//Vortex руты
Route::post('/v_ping', 'Vortex\\VortexController@ping')->name('ping');
Route::post('/v_action', 'Vortex\\VortexController@action')->name('action');
Route::post('/start_vortex', 'Vortex\\VortexController@startVortex')->name('start_vortex');

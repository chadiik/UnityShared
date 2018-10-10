const child_process = require('child_process');

/** @param {string} package @param {Function<error>} callback */
function install(package, callback) {

	function callbackWrap(error, stdout, stderr) {

		process.stdout.write(stdout + '\n');
		process.stderr.write(stderr + '\n');

		if(callback && error != null) callback(error);
	}

	let command = 'npm install ' + package;
	child_process.exec(command, {}, callbackWrap);
}

module.exports = {
	install: install
};
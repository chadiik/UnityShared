const process = require('./process');

const git = 'git+https://github.com/chadiik/UnityShared.git';

/** @type {Array<string>} */
const branches = {
    devUtils: true
};

/** @param {string} branch */
function installBranch(branch){

    if(branches[branch] !== undefined){

        let package = git + '#' + branch;
        process.install(package, function(error){

            if(error) throw error;
        });

        return true;
    }

    return false;
}

module.exports = {
    git: git,
    branches: Object.keys(branches),
    installBranch: installBranch
};